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
    /// This class handles sending twin messages to the client (Get twin responses and
    /// desired properties updates)
    /// It handles receiving links that match the template /devices/{0}/twin or /devices/{0}/modules/{1}/twin
    /// </summary>
    public class TwinSendingLinkHandler : SendingLinkHandler
    {
        public TwinSendingLinkHandler(
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
        }

        public override string CorrelationId =>
            AmqpConnectionUtils.GetCorrelationId(this.Link);

        public override LinkType Type => LinkType.TwinSending;

        protected override QualityOfService QualityOfService => QualityOfService.AtMostOnce;

        protected override async Task OnOpenAsync(TimeSpan timeout)
        {
            await base.OnOpenAsync(timeout);
            await this.DeviceListener.AddSubscription(DeviceSubscription.DesiredPropertyUpdates);
        }
    }
}
