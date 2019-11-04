// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core;

    public class RoutingMessageDeliverable : StandardDeliverable<Message, Core.IMessage, Router>
    {
        private class DeliveryStrategy : IDeliveryStrategy<Message, Core.IMessage, Router>
        {
            public IDeliverableGeneratingStrategy<Message> DefaultDeliverableGeneratingStrategy => new SimpleRoutingMessageGeneratingStrategy();

            public string GetDeliverableId(Message message) => message.SystemProperties[Core.SystemProperties.EdgeMessageId];
            public string GetDeliverableId(Core.IMessage message) => message.SystemProperties[Core.SystemProperties.EdgeMessageId];
            public Task SendDeliverable(Router media, Message message) => media.RouteAsync(message);
        }

        public static RoutingMessageDeliverable Create() => new RoutingMessageDeliverable();

        protected RoutingMessageDeliverable()
            : base(new DeliveryStrategy())
        {
        }
    }
}
