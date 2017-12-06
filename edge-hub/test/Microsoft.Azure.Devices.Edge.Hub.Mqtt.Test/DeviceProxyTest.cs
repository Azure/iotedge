// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Moq;
    using Xunit;
    using IProtocolGatewayMessage = ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class DeviceProxyTest
    {
        class TestDesiredUpdateMessage
        {
            public MqttMessage CoreMessage { get; }

            public ProtocolGatewayMessage PgMessage { get; }

            public TestDesiredUpdateMessage(string desiredJson)
            {
                this.CoreMessage = new MqttMessage.Builder(Encoding.UTF8.GetBytes(desiredJson))
                    .SetSystemProperties(new Dictionary<string, string>()
                    {
                        [SystemProperties.OutboundUri] = Mqtt.Constants.OutboundUriTwinDesiredPropertyUpdate,
                        [SystemProperties.Version] = 1.ToString()
                    })
                    .Build();

                this.PgMessage = new ProtocolGatewayMessage.Builder(Encoding.UTF8.GetBytes(desiredJson).ToByteBuffer(),
                    "$iothub/twin/PATCH/properties/desired/?$version=1").Build();
            }
        }

        [Fact]
        public void OnDesiredPropertyUpdatesSendsAMessageToTheProtocolGateway()
        {
            const string Update = "{\"abc\": \"123\", \"$version\": 1}";
            var message = new TestDesiredUpdateMessage(Update);

            var converter = new Mock<IMessageConverter<IProtocolGatewayMessage>>();
            converter.Setup(x => x.FromMessage(It.IsAny<Core.IMessage>()))
                .Returns(() => message.PgMessage);

            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            channel.Setup(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()));

            var deviceProxy = new DeviceProxy(channel.Object, Mock.Of<IIdentity>(), converter.Object);
            deviceProxy.OnDesiredPropertyUpdates(message.CoreMessage);

            converter.Verify(x => x.FromMessage(It.Is<Core.IMessage>(actualCore => message.CoreMessage.Equals(actualCore))));
            channel.Verify(x => x.Handle(It.Is<IProtocolGatewayMessage>(actualPg => message.PgMessage.Equals(actualPg))));
        }
    }
}
