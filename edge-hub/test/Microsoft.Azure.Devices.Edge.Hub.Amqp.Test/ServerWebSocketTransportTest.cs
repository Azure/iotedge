// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ServerWebSocketTransportTest
    {
        [Fact]
        public void ReadAsyncThrowsWhenBufferIsNull()
        {
            var webSocket = new Mock<WebSocket>();
            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            Assert.Throws<ArgumentNullException>(() => serverTransport.ReadAsync(new TransportAsyncCallbackArgs()));
        }

        [Fact]
        public void ReadAsyncThrowsWhenCompletedCallbackIsNull()
        {
            var webSocket = new Mock<WebSocket>();
            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], 0, 4);
            Assert.Throws<ArgumentNullException>(() => serverTransport.ReadAsync(args));
        }

        [Fact]
        public void ReadAsyncThrowsWhenSocketNotOpen()
        {
            var webSocket = new Mock<WebSocket>();
            webSocket.SetupSequence(ws => ws.State)
                .Returns(WebSocketState.Aborted)
                .Returns(WebSocketState.Closed)
                .Returns(WebSocketState.CloseSent)
                .Returns(WebSocketState.CloseReceived)
                .Returns(WebSocketState.None);
            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], 0, 4);
            args.CompletedCallback += delegate (TransportAsyncCallbackArgs callbackArgs) { };
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<AmqpException>(() => serverTransport.ReadAsync(args));
        }

        [Fact]
        public void ReadAsyncThrowsWhenBufferLimits()
        {
            var webSocket = new Mock<WebSocket>();
            webSocket.Setup(ws => ws.State)
                .Returns(WebSocketState.Open);

            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], -1, 4);
            args.CompletedCallback += delegate { };
            Assert.Throws<ArgumentOutOfRangeException>(() => serverTransport.ReadAsync(args));

            args.SetBuffer(new byte[4], 5, 9);
            Assert.Throws<ArgumentOutOfRangeException>(() => serverTransport.ReadAsync(args));

            args.SetBuffer(new byte[4], 2, -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => serverTransport.ReadAsync(args));

            args.SetBuffer(new byte[4], 2, 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => serverTransport.ReadAsync(args));
        }

        [Fact]
        public void ReadAsyncSuccess()
        {
            var webSocket = new Mock<WebSocket>();
            webSocket.Setup(ws => ws.State)
                .Returns(WebSocketState.Open);
            webSocket.Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new WebSocketReceiveResult(2, WebSocketMessageType.Text, true)));

            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], 0, 4);
            args.CompletedCallback += delegate { };

            Assert.False(serverTransport.ReadAsync(args));
            webSocket.VerifyAll();
        }

        [Fact]
        public void WriteAsyncThrowsWhenBufferIsNull()
        {
            var webSocket = new Mock<WebSocket>();
            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            Assert.Throws<ArgumentException>(() => serverTransport.WriteAsync(new TransportAsyncCallbackArgs()));
        }

        [Fact]
        public void WriteAsyncThrowsWhenCompletedCallbackIsNull()
        {
            var webSocket = new Mock<WebSocket>();
            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], 0, 4);
            Assert.Throws<ArgumentNullException>(() => serverTransport.WriteAsync(args));
        }

        [Fact]
        public void WriteAsyncThrowsWhenSocketNotOpen()
        {
            var webSocket = new Mock<WebSocket>();
            webSocket.SetupSequence(ws => ws.State)
                .Returns(WebSocketState.Aborted)
                .Returns(WebSocketState.Closed)
                .Returns(WebSocketState.CloseSent)
                .Returns(WebSocketState.CloseReceived)
                .Returns(WebSocketState.None);
            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], 0, 4);
            args.CompletedCallback += delegate { };
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<ObjectDisposedException>(() => serverTransport.ReadAsync(args));
            Assert.Throws<AmqpException>(() => serverTransport.WriteAsync(args));
        }

        [Fact]
        public void WriteBufferAsyncSuccess()
        {
            var webSocket = new Mock<WebSocket>();
            webSocket.Setup(ws => ws.State)
                .Returns(WebSocketState.Open);
            webSocket.Setup(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new WebSocketReceiveResult(2, WebSocketMessageType.Text, true)));

            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new byte[4], 0, 4);
            args.CompletedCallback += delegate { };

            Assert.False(serverTransport.WriteAsync(args));
            webSocket.VerifyAll();
        }

        [Fact]
        public void WriteBufferListAsyncSuccess()
        {
            var webSocket = new Mock<WebSocket>();
            webSocket.Setup(ws => ws.State)
                .Returns(WebSocketState.Open);
            webSocket.Setup(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new WebSocketReceiveResult(2, WebSocketMessageType.Text, true)));

            var serverTransport = new ServerWebSocketTransport(webSocket.Object, "local", "remote", Guid.NewGuid().ToString());

            var args = new TransportAsyncCallbackArgs();
            args.SetBuffer(new List<ByteBuffer> { new ByteBuffer(new byte[4]), new ByteBuffer(new byte[5])});
            args.CompletedCallback += delegate { };

            Assert.False(serverTransport.WriteAsync(args));
            webSocket.Verify(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
