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
    /// Address matches the template "/devices/{0}/messages/deviceBound"
    /// </summary>
    public class DeviceBoundLinkHandler : SendingLinkHandler
    {
        public DeviceBoundLinkHandler(
            IIdentity identity,
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter)
        {
        }

        public override LinkType Type => LinkType.C2D;

        protected override QualityOfService QualityOfService => QualityOfService.ExactlyOnce;

        protected override async Task OnOpenAsync(TimeSpan timeout)
        {
            // TODO: Check if we need to worry about credit available on the link
            await base.OnOpenAsync(timeout);

            if (!(this.Identity is IModuleIdentity))
            {
                await this.DeviceListener.AddSubscription(DeviceSubscription.C2D);
            }
        }
    }
}
