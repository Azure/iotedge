// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;

    class PartitionReceiveHandler : IPartitionReceiveHandler
    {
        const string DeviceIdPropertyName = "iothub-connection-device-id";
        const string ModuleIdPropertyName = "iothub-connection-module-id";
        const string SequenceNumberPropertyName = "sequenceNumber";
        const string EnqueuedTimePropertyName = "iothub-enqueuedtime";
        const string BatchIdPropertyName = "batchId";
        static readonly ILogger Log = Logger.Factory.CreateLogger<PartitionReceiveHandler>();
        readonly string deviceId;
        readonly IList<string> excludedModulesIds;

        public PartitionReceiveHandler(string deviceId, IList<string> excludedModulesIds)
        {
            this.deviceId = deviceId;
            this.excludedModulesIds = excludedModulesIds;
        }

        public int MaxBatchSize { get; set; }

        public Task ProcessEventsAsync(IEnumerable<EventData> events)
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
                        eventData.Properties.TryGetValue(SequenceNumberPropertyName, out object sequence);
                        eventData.Properties.TryGetValue(BatchIdPropertyName, out object batchId);

                        if (sequence != null && batchId != null)
                        {
                            DateTime enqueuedtime = DateTime.MinValue.ToUniversalTime();
                            if (eventData.SystemProperties.TryGetValue(EnqueuedTimePropertyName, out object enqueued))
                            {
                                if (DateTime.TryParse(enqueued.ToString(), out enqueuedtime))
                                {
                                    enqueuedtime = DateTime.SpecifyKind(enqueuedtime, DateTimeKind.Utc);
                                }
                            }

                            if (long.TryParse(sequence.ToString(), out long sequenceNumber))
                            {
                                MessagesCache.Instance.AddMessage(modId.ToString(), batchId.ToString(), new MessageDetails(sequenceNumber, enqueuedtime));
                            }
                        }
                        else
                        {
                            Log.LogDebug($"Message for moduleId: {modId} doesn't contain required properties");
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(Exception error)
        {
            Log.LogError(error.StackTrace);
            return Task.CompletedTask;
        }
    }
}
