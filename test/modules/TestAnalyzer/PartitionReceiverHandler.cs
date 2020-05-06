// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;

    class PartitionReceiveHandler : IPartitionReceiveHandler
    {
        const string DeviceIdPropertyName = "iothub-connection-device-id";
        const string ModuleIdPropertyName = "iothub-connection-module-id";
        const string EnqueuedTimePropertyName = "iothub-enqueuedtime";
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(PartitionReceiveHandler));
        readonly string deviceId;
        readonly IList<string> excludedModulesIds;

        public PartitionReceiveHandler(string deviceId, IList<string> excludedModulesIds)
        {
            this.deviceId = deviceId;
            this.excludedModulesIds = excludedModulesIds;
        }

        public int MaxBatchSize { get; set; }

        public async Task ProcessEventsAsync(IEnumerable<EventData> events)
        {
            if (events != null)
            {
                foreach (EventData eventData in events)
                {
                    eventData.SystemProperties.TryGetValue(DeviceIdPropertyName, out object devId);
                    eventData.SystemProperties.TryGetValue(ModuleIdPropertyName, out object modId);

                    if (devId != null && devId.ToString() == this.deviceId &&
                        modId != null && !this.excludedModulesIds.Contains(modId.ToString()))
                    {
                        eventData.Properties.TryGetValue(TestConstants.Message.SequenceNumberPropertyName, out object sequence);
                        eventData.Properties.TryGetValue(TestConstants.Message.BatchIdPropertyName, out object batchId);

                        if (sequence != null && batchId != null)
                        {
                            if (long.TryParse(sequence.ToString(), out long sequenceNumber))
                            {
                                DateTime enqueuedtime = GetEnqueuedTime(devId.ToString(), modId.ToString(), eventData);
                                await ReportingCache.Instance.AddMessageAsync(new MessageDetails(modId.ToString(), batchId.ToString(), sequenceNumber, enqueuedtime));
                            }
                            else
                            {
                                Logger.LogError($"Message for module [{modId}] and device [{this.deviceId}] contains invalid sequence number [{sequence}].");
                            }
                        }
                        else
                        {
                            Logger.LogDebug($"Message for module [{modId}] and device [{this.deviceId}] doesn't contain batch id and sequence number.");
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
