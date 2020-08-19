// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Stream
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LogsStreamRequestHandlerTest
    {
        static readonly string[] TestLogTexts =
        {
            "<6> 2019-02-08 02:23:23.137 +00:00 [INF] - Starting an important module.\n",
            "<7> 2019-02-09 02:23:23.137 +00:00 [DBG] - Some debug log entry.\n",
            "<6> 2019-02-10 02:23:23.137 +00:00 [INF] - Routine log line.\n",
            "<4> 2019-03-08 02:23:23.137 +00:00 [WRN] - Warning, something bad happened.\n",
            "<3> 2019-05-08 02:23:23.137 +00:00 [ERR] - Something really bad happened.\n"
        };

        [Fact]
        public async Task HandleTest()
        {
            // Arrange
            var buffer = DockerFraming.Frame(TestLogTexts);

            string id = "m1";
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(id, true, Option.None<int>(), Option.None<string>(), Option.None<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(buffer));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ModuleRuntimeInfo(id, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()) });

            var logsProcessor = new LogsProcessor(new LogMessageParser("testIotHub", "d1"));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);
            var logRequestItem = new LogRequestItem(id, ModuleLogFilter.Empty);
            var logsStreamRequest = new LogsStreamRequest("1.0", new List<LogRequestItem> { logRequestItem }, LogsContentEncoding.None, LogsContentType.Text);
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
            var logsStreamRequestHandler = new LogsStreamRequestHandler(logsProvider, runtimeInfoProvider.Object);
            await logsStreamRequestHandler.Handle(clientWebSocket.Object, CancellationToken.None);

            // Assert
            runtimeInfoProvider.VerifyAll();
            clientWebSocket.VerifyAll();
            IList<string> receivedChunks = SimpleFraming.Parse(receivedBytes.ToArray())
                .Select(r => Encoding.UTF8.GetString(r))
                .ToList();
            Assert.Equal(TestLogTexts, receivedChunks);
        }

        [Fact]
        public async Task HandleWithCancellationTest()
        {
            // Arrange
            var buffer = DockerFraming.Frame(TestLogTexts);

            string id = "m1";
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(id, true, Option.None<int>(), Option.None<string>(), Option.None<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(buffer));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ModuleRuntimeInfo(id, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()) });

            var logsProcessor = new LogsProcessor(new LogMessageParser("testIotHub", "d1"));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);
            var logRequestItem = new LogRequestItem(id, ModuleLogFilter.Empty);
            var logsStreamRequest = new LogsStreamRequest("1.0", new List<LogRequestItem> { logRequestItem }, LogsContentEncoding.None, LogsContentType.Text);
            byte[] logsStreamRequestBytes = logsStreamRequest.ToBytes();
            var logsStreamRequestArraySeg = new ArraySegment<byte>(logsStreamRequestBytes);
            var clientWebSocket = new Mock<IClientWebSocket>();
            clientWebSocket.Setup(c => c.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, CancellationToken>((a, c) => logsStreamRequestArraySeg.CopyTo(a))
                .ReturnsAsync(new WebSocketReceiveResult(logsStreamRequestBytes.Length, WebSocketMessageType.Binary, true));

            var receivedBytes = new List<byte>();
            clientWebSocket.Setup(c => c.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Binary, true, It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((a, w, f, c) => receivedBytes.AddRange(a.Array))
                .Returns(async () => await Task.Delay(TimeSpan.FromSeconds(3)));
            clientWebSocket.SetupGet(c => c.State).Returns(WebSocketState.Open);

            // Act
            var logsStreamRequestHandler = new LogsStreamRequestHandler(logsProvider, runtimeInfoProvider.Object);
            Task handleTask = logsStreamRequestHandler.Handle(clientWebSocket.Object, CancellationToken.None);

            await Task.Delay(TimeSpan.FromSeconds(10));
            clientWebSocket.SetupGet(c => c.State).Returns(WebSocketState.Closed);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(handleTask.IsCompleted);
            runtimeInfoProvider.VerifyAll();
            clientWebSocket.VerifyAll();

            Assert.True(receivedBytes.Count < buffer.Length);
            IList<string> receivedChunks = SimpleFraming.Parse(receivedBytes.ToArray())
                .Select(r => Encoding.UTF8.GetString(r))
                .ToList();
            Assert.Equal(TestLogTexts.Take(receivedChunks.Count), receivedChunks);
        }
    }
}
