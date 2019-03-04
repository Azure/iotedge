// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Moq;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Mqtt.Constants;

    [Unit]
    public class MqttWebSocketListenerTest
    {
        [Fact]
        public void ReturnsMqttSubProtocol()
        {
            var listener = new MqttWebSocketListener(
                new Settings(Mock.Of<ISettingsProvider>()),
                id => Task.FromResult(Mock.Of<IMessagingBridge>()),
                Mock.Of<IAuthenticator>(),
                Mock.Of<IClientCredentialsFactory>(),
                () => Mock.Of<ISessionStatePersistenceProvider>(),
                new MultithreadEventLoopGroup(),
                Mock.Of<IByteBufferAllocator>(),
                false,
                0,
                true);

            Assert.Equal(Constants.WebSocketSubProtocol, listener.SubProtocol);
        }
    }
}
