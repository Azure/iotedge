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
        const string ModuleIdPropertyName = "moduleId";
        readonly Router router;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;

        public RoutingEdgeHub(Router router, Core.IMessageConverter<IRoutingMessage> messageConverter)
        {
            this.router = router;
            this.messageConverter = messageConverter;
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message)
        {
            this.PopulatePropertiesOnMessage(Preconditions.CheckNotNull(identity, nameof(identity)), 
                Preconditions.CheckNotNull(message, nameof(message)));
            IRoutingMessage routingMessage = this.messageConverter.FromMessage(Preconditions.CheckNotNull(message, nameof(message)));
            return this.router.RouteAsync(routingMessage);
        }

        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> messages)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            IEnumerable<IRoutingMessage> routingMessages = Preconditions.CheckNotNull(messages)
                .Select(
                    m =>
                    {
                        this.PopulatePropertiesOnMessage(identity, m);
                        return this.messageConverter.FromMessage(m);
                    });
            return this.router.RouteAsync(routingMessages);
        }

        void PopulatePropertiesOnMessage(IIdentity identity, IMessage message)
        {
            var moduleIdentity = identity as IModuleIdentity;
            if (moduleIdentity != null)
            {
                message.Properties[ModuleIdPropertyName] = moduleIdentity.ModuleId;
            }
            message.SystemProperties[SystemProperties.DeviceId] = identity.Id;
        }
    }
}