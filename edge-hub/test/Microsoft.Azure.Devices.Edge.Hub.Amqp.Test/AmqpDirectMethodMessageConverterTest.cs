// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    using Constants = Microsoft.Azure.Devices.Edge.Hub.Amqp.Constants;

    [Unit]
    public class AmqpDirectMethodMessageConverterTest
    {
        [Fact]
        public void FromMessageTest()
        {
            // Arrange
            string inputName = "poke";
            string correlationId = Guid.NewGuid().ToString();
            var data = new byte[] { 0, 1, 2 };
            IMessage message = new EdgeMessage.Builder(data)
                .SetSystemProperties(
                    new Dictionary<string, string>
                    {
                        [SystemProperties.CorrelationId] = correlationId
                    })
                .SetProperties(
                    new Dictionary<string, string>
                    {
                        [Constants.MessagePropertiesMethodNameKey] = inputName
                    })
                .Build();
            IMessageConverter<AmqpMessage> messageConverter = new AmqpDirectMethodMessageConverter();

            // Act
            AmqpMessage amqpMessage = messageConverter.FromMessage(message);

            // Assert
            Assert.NotNull(amqpMessage);
            Assert.Equal(data, amqpMessage.GetPayloadBytes());
            Assert.Equal(correlationId, amqpMessage.Properties.CorrelationId.ToString());
            Assert.Equal(inputName, amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesMethodNameKey]);
        }

        [Fact]
        public void ToMessageTest()
        {
            // Arrange
            string correlationId = Guid.NewGuid().ToString();
            var data = new byte[] { 0, 1, 2 };
            AmqpMessage amqpMessage = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(data) });
            amqpMessage.Properties.CorrelationId = correlationId;
            amqpMessage.ApplicationProperties.Map[Constants.MessagePropertiesStatusKey] = 200;
            IMessageConverter<AmqpMessage> messageConverter = new AmqpDirectMethodMessageConverter();

            // Act
            IMessage message = messageConverter.ToMessage(amqpMessage);

            // Assert
            Assert.NotNull(message);
            Assert.Equal(data, message.Body);
            Assert.Equal(2, message.Properties.Count);
            Assert.Equal("200", message.Properties[SystemProperties.StatusCode]);
            Assert.Equal(correlationId, message.Properties[SystemProperties.CorrelationId]);
        }
    }
}
