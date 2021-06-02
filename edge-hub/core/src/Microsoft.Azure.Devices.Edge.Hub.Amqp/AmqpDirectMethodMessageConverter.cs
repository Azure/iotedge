// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    /// <summary>
    /// This converter contains the logic to convert direct method requests/responses to amqp messages
    /// </summary>
    public class AmqpDirectMethodMessageConverter : IMessageConverter<AmqpMessage>
    {
        public AmqpMessage FromMessage(IMessage message)
        {
            AmqpMessage amqpMessage = AmqpMessage.Create(
                new Data
                {
                    Value = new ArraySegment<byte>(message.Body)
                });

            amqpMessage.Properties.CorrelationId = message.SystemProperties[SystemProperties.CorrelationId];
            amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesMethodNameKey] = message.Properties[Constants.MessagePropertiesMethodNameKey];
            return amqpMessage;
        }

        public IMessage ToMessage(AmqpMessage amqpMessage)
        {
            var properties = new Dictionary<string, string>();
            if (amqpMessage.Properties.CorrelationId != null)
            {
                properties[SystemProperties.CorrelationId] = amqpMessage.Properties.CorrelationId.ToString();
            }

            if (amqpMessage.ApplicationProperties.Map.TryGetValue(Constants.MessagePropertiesStatusKey, out int status))
            {
                properties[SystemProperties.StatusCode] = status.ToString();
            }

            byte[] payload = amqpMessage.GetPayloadBytes();

            EdgeMessage message = new EdgeMessage.Builder(payload)
                .SetProperties(properties)
                .Build();
            return message;
        }
    }
}
