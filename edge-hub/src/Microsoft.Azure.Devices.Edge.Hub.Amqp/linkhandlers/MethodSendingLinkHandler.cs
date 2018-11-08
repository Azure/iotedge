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
    /// This handles direct method requests to the client.
    /// It handles links that match the template /devices/{0}/methods/deviceBound or
    /// /devices/{0}/modules/{1}/methods/deviceBound
    /// </summary>
    public class MethodSendingLinkHandler : SendingLinkHandler
    {
        public MethodSendingLinkHandler(
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
        }

        public override string CorrelationId =>
            AmqpConnectionUtils.GetCorrelationId(this.Link);

        public override LinkType Type => LinkType.MethodSending;

        protected override QualityOfService QualityOfService => QualityOfService.AtMostOnce;

        protected override async Task OnOpenAsync(TimeSpan timeout)
        {
            await base.OnOpenAsync(timeout);
            await this.DeviceListener.AddSubscription(DeviceSubscription.Methods);
        }
    }
}
