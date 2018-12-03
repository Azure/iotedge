// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    /// <summary>
    /// This class handles direct method responses from the client.
    /// It handles links that match the template /devices/{0}/methods/deviceBound or
    /// /devices/{0}/modules/{1}/methods/deviceBound
    /// </summary>
    public class MethodReceivingLinkHandler : ReceivingLinkHandler
    {
        public MethodReceivingLinkHandler(
            IIdentity identity,
            IReceivingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter)
        {
        }

        public override LinkType Type => LinkType.MethodReceiving;

        protected override QualityOfService QualityOfService => QualityOfService.AtMostOnce;

        public override string CorrelationId =>
            AmqpConnectionUtils.GetCorrelationId(this.Link);

        protected override async Task OnMessageReceived(AmqpMessage amqpMessage)
        {
            IMessage message = this.MessageConverter.ToMessage(amqpMessage);
            await this.DeviceListener.ProcessMethodResponseAsync(message);
        }
    }
}
