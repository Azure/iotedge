// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public static class MessageQueueIdHelper
    {
        public const string MessageQueueIdDelimiter = "_Pri";

        // The actual ID for the underlying store is of string format: <endpointId>_Pri<priority>
        // We need to maintain backwards compatibility for existing sequential stores that don't have the "_Pri<x>" suffix.
        // We use the default priority (2,000,000,000) for this, which means the store ID is just the endpoint ID.
        public static string GetMessageQueueId(string endpointId, uint priority) => priority == RouteFactory.DefaultPriority ? endpointId : $"{endpointId}{MessageQueueIdDelimiter}{priority}";

        public static (string, uint) ParseMessageQueueId(string messageQueueId)
        {
            var idx = messageQueueId.LastIndexOf(MessageQueueIdDelimiter);
            if (idx < 0)
            {
                return (messageQueueId, RouteFactory.DefaultPriority);
            }

            var endpointId = messageQueueId.Substring(0, idx);
            var priority = messageQueueId.Substring(idx + MessageQueueIdDelimiter.Length);
            if (uint.TryParse(priority, out var priorityNum))
            {
                return (endpointId, priorityNum);
            }
            else
            {
                return (messageQueueId, RouteFactory.DefaultPriority);
            }
        }
    }
}
