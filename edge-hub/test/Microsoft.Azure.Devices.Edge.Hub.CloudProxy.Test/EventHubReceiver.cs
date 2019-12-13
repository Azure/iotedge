// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.EventHubs;

    public class EventHubReceiver
    {
        readonly string eventHubConnectionString;

        public EventHubReceiver(string eventHubConnectionString)
        {
            this.eventHubConnectionString = eventHubConnectionString;
        }

        public async Task<IList<EventData>> GetMessagesForDevice(string deviceId, DateTime startTime, int maxPerPartition = 10, int waitTimeSecs = 5)
        {
            var messages = new List<EventData>();

            // Retry a few times to make sure we get all expected messages.
            for (int i = 0; i < 3; i++)
            {
                EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(this.eventHubConnectionString);
                PartitionReceiver partitionReceiver = eventHubClient.CreateReceiver(
                    PartitionReceiver.DefaultConsumerGroupName,
                    EventHubPartitionKeyResolver.ResolveToPartition(deviceId, (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                    EventPosition.FromEnqueuedTime(startTime));

                IEnumerable<EventData> events = await partitionReceiver.ReceiveAsync(maxPerPartition, TimeSpan.FromSeconds(waitTimeSecs));
                if (events != null)
                {
                    messages.AddRange(events);
                }

                await partitionReceiver.CloseAsync();
                await eventHubClient.CloseAsync();

                if (i < 3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20));
                }
            }

            return messages;
        }
    }
}
