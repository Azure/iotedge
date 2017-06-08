// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class ProtocolGatewayMessageConverterTest
    {
        static readonly DotNetty.Buffers.IByteBuffer Payload = new byte[] { 1, 2, 3 }.ToByteBuffer();

        [Fact]
        public void TestToMessage()
        {
            var outputTemplates = new Dictionary<string, string>
            {
                ["Dummy"] = ""
            };
            var inputTemplates = new List<string>
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/"
            };
            var config = new MessageAddressConversionConfiguration(
                inputTemplates,
                outputTemplates
            );
            var converter = new MessageAddressConverter(config);
            var properties = new Dictionary<string, string>();
            var protocolGatewayMessage = Mock.Of<IProtocolGatewayMessage>(
                m => 
                    m.Address == @"devices/Device_6/messages/events/%24.cid=Corrid1&%24.mid=MessageId1&Foo=Bar&Prop2=Value2&Prop3=Value3/" &&
                    m.Payload == Payload &&
                    m.Properties == properties
                );

            var protocolGatewayMessageConverter = new ProtocolGatewayMessageConverter(converter);
            IMessage message = protocolGatewayMessageConverter.ToMessage(protocolGatewayMessage);
            Assert.NotNull(message);

            Assert.Equal(3, message.SystemProperties.Count);
            Assert.Equal("Corrid1", message.SystemProperties[SystemProperties.CorrelationId]);
            Assert.Equal("MessageId1", message.SystemProperties[SystemProperties.MessageId]);
            Assert.Equal("Device_6", message.SystemProperties[SystemProperties.DeviceId]);

            Assert.Equal(3, message.Properties.Count);
            Assert.Equal("Bar", message.Properties["Foo"]);
            Assert.Equal("Value2", message.Properties["Prop2"]);
            Assert.Equal("Value3", message.Properties["Prop3"]);
        }
    }
}
