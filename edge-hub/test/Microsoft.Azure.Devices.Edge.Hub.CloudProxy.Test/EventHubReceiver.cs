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
        const string EventHubConsumerGroup = "ci-tests";
        readonly string eventHubConnectionString;

        public EventHubReceiver(string eventHubConnectionString)
        {
            this.eventHubConnectionString = eventHubConnectionString;
        }

        public async Task<List<EventData>> GetMessagesForDevice(string deviceId, DateTime startTime, int maxPerPartition = 100, int waitTimeSecs = 5)
        {
            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(this.eventHubConnectionString);
            PartitionReceiver partitionReceiver = eventHubClient.CreateReceiver(
                EventHubConsumerGroup,
                EventHubPartitionKeyResolver.ResolveToPartition(deviceId, (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(startTime));

            List<EventData> messages = (List<EventData>)await partitionReceiver.ReceiveAsync(maxPerPartition, TimeSpan.FromSeconds(waitTimeSecs));

            await partitionReceiver.CloseAsync();
            await eventHubClient.CloseAsync();

            return messages;
        }
    }
}
