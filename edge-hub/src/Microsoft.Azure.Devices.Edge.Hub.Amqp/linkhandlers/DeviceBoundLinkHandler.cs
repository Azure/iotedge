// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    /// <summary>
    /// Address matches the template "/devices/{0}/messages/deviceBound"
    /// </summary>
    public class DeviceBoundLinkHandler : SendingLinkHandler
    {
        public DeviceBoundLinkHandler(
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
        }

        public override LinkType Type => LinkType.C2D;

        protected override QualityOfService QualityOfService => QualityOfService.ExactlyOnce;

        protected override async Task OnOpenAsync(TimeSpan timeout)
        {
            // TODO: Check if we need to worry about credit available on the link
            await base.OnOpenAsync(timeout);

            // TODO: Temporary fix since SDK subscribes to C2D messages for modules.
            if (string.IsNullOrWhiteSpace(this.ModuleId))
            {
                await this.DeviceListener.AddSubscription(DeviceSubscription.C2D);
            }
        }
    }
}
