// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LogsProviderTest
    {
        static readonly string[] TestLogTexts = new[]
        {
            "<6> 2019-02-08 02:23:23.137 +00:00 [INF] - Starting an important module.\n",
            "<7> 2019-02-09 02:23:23.137 +00:00 [DBG] - Some debug log entry.\n",
            "<6> 2019-02-10 02:23:23.137 +00:00 [INF] - Routine log line.\n",
            "<4> 2019-03-08 02:23:23.137 +00:00 [WRN] - Warning, something bad happened.\n",
            "<3> 2019-05-08 02:23:23.137 +00:00 [ERR] - Something really bad happened.\n"
        };

        [Fact]
        public async Task GetLogsAsTextTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.None<int>();
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;
            string expectedLogText = TestLogTexts.Join(string.Empty);

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(GetDockerLogsStream(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.None, LogsContentType.Text);

            // Act
            byte[] bytes = await logsProvider.GetLogs(logOptions, cancellationToken);

            // Assert
            string logsText = Encoding.UTF8.GetString(bytes);
            Assert.Equal(expectedLogText, logsText);
        }

        [Fact]
        public async Task GetLogsAsTextWithCompressionTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.None<int>();
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;
            string expectedLogText = TestLogTexts.Join(string.Empty);

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(GetDockerLogsStream(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.Gzip, LogsContentType.Text);

            // Act
            byte[] bytes = await logsProvider.GetLogs(logOptions, cancellationToken);

            // Assert
            byte[] decompressedBytes = Compression.DecompressFromGzip(bytes);
            string logsText = Encoding.UTF8.GetString(decompressedBytes);
            Assert.Equal(expectedLogText, logsText);
        }

        [Fact]
        public async Task GetLogsAsJsonTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.None<int>();
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(GetDockerLogsStream(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.None, LogsContentType.Json);

            // Act
            byte[] bytes = await logsProvider.GetLogs(logOptions, cancellationToken);

            // Assert
            var logMessages = bytes.FromBytes<List<ModuleLogMessage>>();
            Assert.NotNull(logMessages);
            Assert.Equal(TestLogTexts.Length, logMessages.Count);
            for (int i = 0; i < logMessages.Count; i++)
            {
                ModuleLogMessage logMessage = logMessages[i];
                (int logLevel, Option<DateTime> timeStamp, string text) = LogMessageParser.ParseLogText(TestLogTexts[i]);
                Assert.Equal(logLevel, logMessage.LogLevel);
                Assert.Equal(timeStamp.HasValue, logMessage.TimeStamp.HasValue);
                Assert.Equal(timeStamp.OrDefault(), logMessage.TimeStamp.OrDefault());
                Assert.Equal(text, logMessage.Text);
                Assert.Equal(iotHub, logMessage.IoTHub);
                Assert.Equal(deviceId, logMessage.DeviceId);
                Assert.Equal(moduleId, logMessage.ModuleId);
            }
        }

        [Fact]
        public async Task GetLogsAsJsonWithCompressionTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.None<int>();
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(GetDockerLogsStream(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.Gzip, LogsContentType.Json);

            // Act
            byte[] bytes = await logsProvider.GetLogs(logOptions, cancellationToken);

            // Assert
            byte[] decompressedBytes = Compression.DecompressFromGzip(bytes);
            var logMessages = decompressedBytes.FromBytes<List<ModuleLogMessage>>();
            Assert.NotNull(logMessages);
            Assert.Equal(TestLogTexts.Length, logMessages.Count);
            for (int i = 0; i < logMessages.Count; i++)
            {
                ModuleLogMessage logMessage = logMessages[i];
                (int logLevel, Option<DateTime> timeStamp, string text) = LogMessageParser.ParseLogText(TestLogTexts[i]);
                Assert.Equal(logLevel, logMessage.LogLevel);
                Assert.Equal(timeStamp.HasValue, logMessage.TimeStamp.HasValue);
                Assert.Equal(timeStamp.OrDefault(), logMessage.TimeStamp.OrDefault());
                Assert.Equal(text, logMessage.Text);
                Assert.Equal(iotHub, logMessage.IoTHub);
                Assert.Equal(deviceId, logMessage.DeviceId);
                Assert.Equal(moduleId, logMessage.ModuleId);
            }
        }

        [Fact]
        public async Task GetLogsStreamTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.None<int>();
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            byte[] dockerLogsStreamBytes = GetDockerLogsStream(TestLogTexts);
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.None, LogsContentType.Text);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProvider.GetLogsStream(logOptions, Callback, cancellationToken);

            // Assert
            Assert.NotEmpty(receivedBytes);
            Assert.Equal(dockerLogsStreamBytes, receivedBytes);
        }

        static byte[] GetDockerLogsStream(IEnumerable<string> logTexts)
        {
            byte streamByte = 01;
            var padding = new byte[3];
            var outputBytes = new List<byte>();
            foreach (string text in logTexts)
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                byte[] lenBytes = GetLengthBytes(textBytes.Length);
                outputBytes.Add(streamByte);
                outputBytes.AddRange(padding);
                outputBytes.AddRange(lenBytes);
                outputBytes.AddRange(textBytes);
            }

            return outputBytes.ToArray();
        }

        static byte[] GetLengthBytes(int len)
        {
            byte[] intBytes = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(intBytes);
            }

            byte[] result = intBytes;
            return result;
        }
    }
}
