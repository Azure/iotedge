// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ServerWebSocketChannelTest
    {
        [Fact]
        public void CtorThrowsWhenWebSocketIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServerWebSocketChannel(null, Mock.Of<EndPoint>())
                );
        }

        [Fact]
        public void CtorThrowsWhenEndpointIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServerWebSocketChannel(Mock.Of<WebSocket>(), null)
                );
        }

        [Fact]
        public async Task CanCloseTheChannel()
        {
            var webSocket = Mock.Of<WebSocket>(ws => ws.State == WebSocketState.Open);
            var channel = new ServerWebSocketChannel(webSocket, Mock.Of<EndPoint>());
            var loop = new SingleThreadEventLoop();
            await loop.RegisterAsync(channel);

            await channel.CloseAsync();

            Assert.False(channel.Active);
            Mock.Get(webSocket).Verify(ws =>
                ws.CloseAsync(WebSocketCloseStatus.NormalClosure, It.IsAny<string>(), It.IsAny<CancellationToken>())
                );
        }

        [Fact]
        public async Task DoesNotCloseAnAlreadyClosedWebSocket()
        {
            var webSocket = Mock.Of<WebSocket>(ws => ws.State == WebSocketState.Open);
            var channel = new ServerWebSocketChannel(webSocket, Mock.Of<EndPoint>());
            var loop = new SingleThreadEventLoop();
            await loop.RegisterAsync(channel);

            Mock<WebSocket> wsMock = Mock.Get(webSocket);
            wsMock.Setup(ws => ws.State).Returns(WebSocketState.Closed);
            wsMock.Setup(ws => ws.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("WebSocket.CloseAsync should not be called"));

            await channel.CloseAsync();
        }
    }
}
