// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Threading.Tasks;

    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    using Moq;

    using Xunit;

    [Unit]
    public class MqttWebSocketListenerTest
    {
        [Fact]
        public void ReturnsMqttSubProtocol()
        {
            var listener = new MqttWebSocketListener(
                new Settings(Mock.Of<ISettingsProvider>()),
                id => Task.FromResult(Mock.Of<IMessagingBridge>()),
                Mock.Of<IDeviceIdentityProvider>(),
                () => Mock.Of<ISessionStatePersistenceProvider>(),
                new MultithreadEventLoopGroup(),
                Mock.Of<IByteBufferAllocator>(),
                false,
                0);

            Assert.Equal(Constants.WebSocketSubProtocol, listener.SubProtocol);
        }
    }
}
