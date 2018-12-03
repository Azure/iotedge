// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    /// <summary>
    /// This handler is used to send messages to modules
    /// Address matches the template "/devices/{deviceid}/modules/{moduleid}/messages/events"
    /// </summary>
    class ModuleMessageLinkHandler : SendingLinkHandler
    {
        public ModuleMessageLinkHandler(
            IIdentity identity,
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter)
        {
        }

        public override LinkType Type => LinkType.ModuleMessages;

        protected override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;

        protected override async Task OnOpenAsync(TimeSpan timeout)
        {
            await base.OnOpenAsync(timeout);
            await this.DeviceListener.AddSubscription(DeviceSubscription.ModuleMessages);
        }
    }
}
