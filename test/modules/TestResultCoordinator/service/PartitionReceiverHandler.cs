// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Storage;
    using TestOperationResult = TestResultCoordinator.TestOperationResult;

    class PartitionReceiveHandler : IPartitionReceiveHandler
    {
        const string DeviceIdPropertyName = "iothub-connection-device-id";
        const string ModuleIdPropertyName = "iothub-connection-module-id";
        const string EnqueuedTimePropertyName = "iothub-enqueuedtime";

        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(PartitionReceiveHandler));

        readonly string deviceId;
        readonly string trackingId;
        readonly ITestOperationResultStorage storage;

        public PartitionReceiveHandler(string trackingId, string deviceId, ITestOperationResultStorage storage)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
        }

        public int MaxBatchSize { get; set; }

        public async Task ProcessEventsAsync(IEnumerable<EventData> events)
        {
            Logger.LogInformation("Processing events from event hub.");

            if (events != null)
            {
                foreach (EventData eventData in events)
                {
                    eventData.Properties.TryGetValue(TestConstants.Message.TrackingIdPropertyName, out object trackingIdFromEvent);
                    eventData.SystemProperties.TryGetValue(DeviceIdPropertyName, out object deviceIdFromEvent);
                    eventData.SystemProperties.TryGetValue(ModuleIdPropertyName, out object moduleIdFromEvent);

                    Logger.LogDebug($"Received event from Event Hub: trackingId={(string)trackingIdFromEvent}, deviceId={(string)deviceIdFromEvent}, moduleId={(string)moduleIdFromEvent}");

                    if (!string.IsNullOrWhiteSpace((string)trackingIdFromEvent) &&
                        string.Equals(trackingIdFromEvent.ToString(), this.trackingId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace((string)deviceIdFromEvent) &&
                        string.Equals(deviceIdFromEvent.ToString(), this.deviceId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace((string)moduleIdFromEvent))
                    {
                        eventData.Properties.TryGetValue(TestConstants.Message.SequenceNumberPropertyName, out object sequenceNumberFromEvent);
                        eventData.Properties.TryGetValue(TestConstants.Message.BatchIdPropertyName, out object batchIdFromEvent);

                        Logger.LogDebug($"Received event from Event Hub: batchId={(string)batchIdFromEvent}, sequenceNumber={(string)sequenceNumberFromEvent}");

                        if (!string.IsNullOrWhiteSpace((string)sequenceNumberFromEvent) &&
                            !string.IsNullOrWhiteSpace((string)batchIdFromEvent))
                        {
                            if (long.TryParse(sequenceNumberFromEvent.ToString(), out long sequenceNumber))
                            {
                                DateTime enqueuedtime = GetEnqueuedTime(deviceIdFromEvent.ToString(), moduleIdFromEvent.ToString(), eventData);

                                // TODO: remove hardcoded eventHub string in next line
                                var result = new TestOperationResult(
                                    (string)moduleIdFromEvent + ".eventHub",
                                    TestOperationResultType.Messages.ToString(),
                                    ModuleUtil.FormatMessagesTestResultValue(
                                        (string)trackingIdFromEvent,
                                        (string)batchIdFromEvent,
                                        (string)sequenceNumberFromEvent),
                                    enqueuedtime);
                                await this.storage.AddResultAsync(result);
                                Logger.LogInformation($"Received event from Event Hub persisted to store: trackingId={(string)trackingIdFromEvent}, deviceId={(string)deviceIdFromEvent}, moduleId={(string)moduleIdFromEvent}, batchId={(string)batchIdFromEvent}, sequenceNumber={(string)sequenceNumberFromEvent}");
                            }
                            else
                            {
                                Logger.LogError($"Message for module [{moduleIdFromEvent}] and device [{this.deviceId}] contains invalid sequence number [{(string)sequenceNumberFromEvent}].");
                            }
                        }
                        else
                        {
                            Logger.LogDebug($"Message for module [{moduleIdFromEvent}] and device [{this.deviceId}] doesn't contain batch id and/or sequence number.");
                        }
                    }
                }
            }
        }

        public Task ProcessErrorAsync(Exception error)
        {
            Logger.LogError(error.ToString());
            return Task.CompletedTask;
        }

        static DateTime GetEnqueuedTime(string deviceId, string moduleId, EventData eventData)
        {
            DateTime enqueuedtime = DateTime.MinValue.ToUniversalTime();

            if (eventData.SystemProperties.TryGetValue(EnqueuedTimePropertyName, out object enqueued))
            {
                if (DateTime.TryParse(enqueued.ToString(), out enqueuedtime))
                {
                    enqueuedtime = DateTime.SpecifyKind(enqueuedtime, DateTimeKind.Utc);
                }
                else
                {
                    Logger.LogError($"Message for module [{moduleId}] and device [{deviceId}] enqueued time [{enqueued}] cannot be parsed.");
                }
            }
            else
            {
                Logger.LogError($"Message for module [{moduleId}] and device [{deviceId}] doesn't contain {EnqueuedTimePropertyName} property.");
            }

            return enqueuedtime;
        }
    }
}
