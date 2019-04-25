// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
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

        public int MaxBatchSize { get; set; }

        public Task ProcessEventsAsync(IEnumerable<EventData> events)
        {
            if (events != null)
            {
                foreach (EventData data in events)
                {
                    if (this.onEventReceived(data))
                    {
                        break;
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(Exception error) => throw error;
    }
}
