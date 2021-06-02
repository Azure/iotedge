// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This converter contains the logic to convert twin messages to amqp messages
    /// </summary>
    public class AmqpTwinMessageConverter : IMessageConverter<AmqpMessage>
    {
        public AmqpMessage FromMessage(IMessage message)
        {
            AmqpMessage amqpMessage = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(message.Body) });
            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.CorrelationId, out string correlationId))
            {
                amqpMessage.Properties.CorrelationId = correlationId;
            }

            if (message.SystemProperties.TryGetNonEmptyValue(SystemProperties.StatusCode, out string statusCode)
                && int.TryParse(statusCode, out int status))
            {
                amqpMessage.MessageAnnotations.Map["status"] = status;
            }

            return amqpMessage;
        }

        public IMessage ToMessage(AmqpMessage amqpMessage)
        {
            IMessage message = new EdgeMessage.Builder(amqpMessage.GetPayloadBytes()).Build();
            return message;
        }
    }
}
