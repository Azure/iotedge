// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Identity;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Primitives;
    using Microsoft.Azure.Devices.Edge.Test.Common;

    public class EventHubReceiver
    {
        const string EventHubConsumerGroup = "ci-tests";
        readonly string eventHubNamespace;
        readonly string eventHubName;

        public EventHubReceiver(string eventHubNamespace, string eventHubName)
        {
            this.eventHubNamespace = eventHubNamespace;
            this.eventHubName = eventHubName;
        }

        public async Task<List<EventData>> GetMessagesForDevice(string deviceId, DateTime startTime, int maxPerPartition = 100, int waitTimeSecs = 5)
        {
            var messages = new List<EventData>();

            int eventHubPartitionCount;
            await using (var consumer = new EventHubConsumerClient(
                EventHubConsumerClient.DefaultConsumerGroupName,
                this.eventHubNamespace,
                this.eventHubName,
                new AzureCliCredential(),
                new EventHubConsumerClientOptions()))
            {
                eventHubPartitionCount = (await consumer.GetPartitionIdsAsync()).Length;
            }

            await using (var receiver = new PartitionReceiver(
                EventHubConsumerClient.DefaultConsumerGroupName,
                EventHubPartitionKeyResolver.ResolveToPartition(deviceId, eventHubPartitionCount),
                EventPosition.FromEnqueuedTime(startTime),
                this.eventHubNamespace,
                this.eventHubName,
                new AzureCliCredential()))
            {
                // Retry a few times due to weird behavior with ReceiveAsync() not returning all messages available
                for (int i = 0; i < 3; i++)
                {
                    IEnumerable<EventData> events = await receiver.ReceiveBatchAsync(maxPerPartition, TimeSpan.FromSeconds(waitTimeSecs), CancellationToken.None);
                    if (events != null)
                    {
                        messages.AddRange(events);
                    }

                    if (i < 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
            }

            return messages;
        }
    }
}
