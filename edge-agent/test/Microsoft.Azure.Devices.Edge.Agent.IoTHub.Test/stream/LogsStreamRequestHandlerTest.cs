// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Stream
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LogsStreamRequestHandlerTest
    {
        [Fact]
        public async Task HandleTest()
        {
            // Arrange
            var random = new Random();
            var buffer = new byte[1024 * 128];
            random.NextBytes(buffer);

            string id = "m1";
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(id, true, Option.None<int>(), Option.None<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(buffer));

            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, Mock.Of<ILogsProcessor>());

            var logsStreamRequest = new LogsStreamRequest("1.0", id);
            byte[] logsStreamRequestBytes = logsStreamRequest.ToBytes();
            var logsStreamRequestArraySeg = new ArraySegment<byte>(logsStreamRequestBytes);
            var clientWebSocket = new Mock<IClientWebSocket>();
            clientWebSocket.Setup(c => c.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, CancellationToken>((a, c) => logsStreamRequestArraySeg.CopyTo(a))
                .Returns(
                    async () =>
                    {
                        await Task.Yield();
                        return new WebSocketReceiveResult(logsStreamRequestBytes.Length, WebSocketMessageType.Binary, true);
                    });
            var receivedBytes = new List<byte>();
            clientWebSocket.Setup(c => c.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Binary, true, It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((a, w, f, c) => receivedBytes.AddRange(a.Array))
                .Returns(async () => await Task.Yield());
            clientWebSocket.SetupGet(c => c.State).Returns(WebSocketState.Open);

            // Act
            var logsStreamRequestHandler = new LogsStreamRequestHandler(logsProvider);
            await logsStreamRequestHandler.Handle(clientWebSocket.Object, CancellationToken.None);

            // Assert
            runtimeInfoProvider.VerifyAll();
            clientWebSocket.VerifyAll();
            Assert.Equal(buffer, receivedBytes.ToArray());
        }

        [Fact]
        public async Task HandleWithCancellationTest()
        {
            // Arrange
            var random = new Random();
            var buffer = new byte[1024 * 1024];
            random.NextBytes(buffer);

            string id = "m1";
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(id, true, Option.None<int>(), Option.None<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(buffer));

            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, Mock.Of<ILogsProcessor>());

            var logsStreamRequest = new LogsStreamRequest("1.0", id);
            byte[] logsStreamRequestBytes = logsStreamRequest.ToBytes();
            var logsStreamRequestArraySeg = new ArraySegment<byte>(logsStreamRequestBytes);
            var clientWebSocket = new Mock<IClientWebSocket>();
            clientWebSocket.Setup(c => c.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, CancellationToken>((a, c) => logsStreamRequestArraySeg.CopyTo(a))
                .ReturnsAsync(new WebSocketReceiveResult(logsStreamRequestBytes.Length, WebSocketMessageType.Binary, true));

            var receivedBytes = new List<byte>();
            clientWebSocket.Setup(c => c.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Binary, true, It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((a, w, f, c) => receivedBytes.AddRange(a.Array))
                .Returns(async () => await Task.Delay(TimeSpan.FromSeconds(1)));
            clientWebSocket.SetupGet(c => c.State).Returns(WebSocketState.Open);

            // Act
            var logsStreamRequestHandler = new LogsStreamRequestHandler(logsProvider);
            Task handleTask = logsStreamRequestHandler.Handle(clientWebSocket.Object, CancellationToken.None);

            await Task.Delay(TimeSpan.FromSeconds(10));
            clientWebSocket.SetupGet(c => c.State).Returns(WebSocketState.Closed);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(handleTask.IsCompleted);
            runtimeInfoProvider.VerifyAll();
            clientWebSocket.VerifyAll();

            Assert.True(receivedBytes.Count < buffer.Length);
            Assert.Equal(new ArraySegment<byte>(buffer, 0, receivedBytes.Count).ToArray(), receivedBytes.ToArray());
        }
    }
}
