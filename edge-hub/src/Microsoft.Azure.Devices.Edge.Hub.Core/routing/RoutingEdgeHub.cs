// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class RoutingEdgeHub : IEdgeHub
    {
        readonly Router router;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;

        public RoutingEdgeHub(Router router, Core.IMessageConverter<IRoutingMessage> messageConverter)
        {
            this.router = router;
            this.messageConverter = messageConverter;
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message)
        {
            IRoutingMessage routingMessage = this.messageConverter.FromMessage(Preconditions.CheckNotNull(message, nameof(message)));
            return this.router.RouteAsync(routingMessage);
        }

        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> messages)
        {
            IEnumerable<IRoutingMessage> routingMessages = Preconditions.CheckNotNull(messages)
                .Select(m => this.messageConverter.FromMessage(m));
            return this.router.RouteAsync(routingMessages);
        }
    }
}