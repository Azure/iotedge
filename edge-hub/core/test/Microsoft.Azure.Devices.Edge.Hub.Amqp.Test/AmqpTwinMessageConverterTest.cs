// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Xunit;

    [Unit]
    public class AmqpTwinMessageConverterTest
    {
        [Fact]
        public void FromMessageTest()
        {
            // Arrange
            var collection = new TwinCollection()
            {
                ["prop"] = "value",
                ["$version"] = 1
            };
            string correlationId = Guid.NewGuid().ToString();
            byte[] data = Encoding.UTF8.GetBytes(collection.ToJson());
            IMessage message = new EdgeMessage.Builder(data)
                .SetSystemProperties(
                    new Dictionary<string, string>
                    {
                        [SystemProperties.CorrelationId] = correlationId
                    })
                .Build();
            IMessageConverter<AmqpMessage> messageConverter = new AmqpTwinMessageConverter();

            // Act
            AmqpMessage amqpMessage = messageConverter.FromMessage(message);

            // Assert
            Assert.NotNull(amqpMessage);
            Assert.Equal(data, amqpMessage.GetPayloadBytes());
            Assert.Equal(correlationId, amqpMessage.Properties.CorrelationId.ToString());
        }

        [Fact]
        public void ToMessageTest()
        {
            // Arrange
            var collection = new TwinCollection()
            {
                ["prop"] = "value",
                ["$version"] = 1
            };

            byte[] data = Encoding.UTF8.GetBytes(collection.ToJson());
            AmqpMessage amqpMessage = AmqpMessage.Create(new Data { Value = new ArraySegment<byte>(data) });
            IMessageConverter<AmqpMessage> messageConverter = new AmqpTwinMessageConverter();

            // Act
            IMessage message = messageConverter.ToMessage(amqpMessage);

            // Assert
            Assert.NotNull(message);
            Assert.Equal(data, message.Body);
        }
    }
}
