// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Moq;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Mqtt.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    [Unit]
    public class DeviceProxyTest
    {
        static readonly IByteBufferConverter ByteBufferConverter = new ByteBufferConverter(PooledByteBufferAllocator.Default);

        [Fact]
        public void OnDesiredPropertyUpdatesSendsAMessageToTheProtocolGateway()
        {
            const string Update = "{\"abc\": \"123\", \"$version\": 1}";
            var message = new TestDesiredUpdateMessage(Update);

            var converter = new Mock<IMessageConverter<IProtocolGatewayMessage>>();
            converter.Setup(x => x.FromMessage(It.IsAny<IMessage>()))
                .Returns(() => message.PgMessage);

            var channel = new Mock<IMessagingChannel<IProtocolGatewayMessage>>();
            channel.Setup(x => x.Handle(It.IsAny<IProtocolGatewayMessage>()));

            var deviceProxy = new DeviceProxy(channel.Object, Mock.Of<IIdentity>(), converter.Object, ByteBufferConverter);
            deviceProxy.OnDesiredPropertyUpdates(message.CoreMessage);

            converter.Verify(x => x.FromMessage(It.Is<IMessage>(actualCore => message.CoreMessage.Equals(actualCore))));
            channel.Verify(x => x.Handle(It.Is<IProtocolGatewayMessage>(actualPg => message.PgMessage.Equals(actualPg))));
        }

        class TestDesiredUpdateMessage
        {
            public TestDesiredUpdateMessage(string desiredJson)
            {
                this.CoreMessage = new EdgeMessage.Builder(Encoding.UTF8.GetBytes(desiredJson))
                    .SetSystemProperties(
                        new Dictionary<string, string>()
                        {
                            [SystemProperties.OutboundUri] = Constants.OutboundUriTwinDesiredPropertyUpdate,
                            [SystemProperties.Version] = 1.ToString()
                        })
                    .Build();

                this.PgMessage = new ProtocolGatewayMessage.Builder(
                    ByteBufferConverter.ToByteBuffer(Encoding.UTF8.GetBytes(desiredJson)),
                    "$iothub/twin/PATCH/properties/desired/?$version=1").Build();
            }

            public EdgeMessage CoreMessage { get; }

            public ProtocolGatewayMessage PgMessage { get; }
        }
    }
}
