// Copyright (c) Microsoft. All rights reserved.
namespace LeafDevice.Details
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;

    class PartitionReceiveHandler : IPartitionReceiveHandler
    {
        readonly Func<EventData, bool> onEventReceived;
        public PartitionReceiveHandler(Func<EventData, bool> onEventReceived)
        {
            this.onEventReceived = onEventReceived;
        }
        public Task ProcessEventsAsync(IEnumerable<EventData> events)
        {
            if (events != null)
            {
                foreach (EventData @event in events)
                {
                    if (this.onEventReceived(@event)) break;
                }
            }
            return Task.CompletedTask;
        }
        public Task ProcessErrorAsync(Exception error) => throw error;
        public int MaxBatchSize { get; set; }
    }
}
