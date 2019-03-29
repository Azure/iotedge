// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        static readonly string[] TestLogTexts =
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
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty);

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
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty);

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
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty);

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
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty);

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

            byte[] dockerLogsStreamBytes = DockerFraming.Frame(TestLogTexts);
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty);

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

        [Fact]
        public async Task GetLogsStreamWithFiltersTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.Some(10);
            Option<int> since = Option.Some(1552887267);
            CancellationToken cancellationToken = CancellationToken.None;

            byte[] dockerLogsStreamBytes = DockerFraming.Frame(TestLogTexts);
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var filter = new ModuleLogFilter(tail, since, Option.Some(6), Option.Some("Starting"));
            var logOptions = new ModuleLogOptions(moduleId, LogsContentEncoding.Gzip, LogsContentType.Text, filter);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProvider.GetLogsStream(logOptions, Callback, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert
            Assert.NotEmpty(receivedBytes);
            string receivedText = Compression.DecompressFromGzip(receivedBytes.ToArray())
                .Skip(8)
                .ToArray()
                .FromBytes();
            Assert.Equal(TestLogTexts[0], receivedText);
        }

        [Theory]
        [MemberData(nameof(GetNeedToProcessStreamTestData))]
        public void NeedToProcessStreamTest(ModuleLogOptions logOptions, bool expectedResult)
        {
            Assert.Equal(expectedResult, LogsProvider.NeedToProcessStream(logOptions));
        }

        public static IEnumerable<object[]> GetNeedToProcessStreamTestData()
        {
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty), false };
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.None<int>(), Option.None<string>())), false };
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty), true };
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.Some(3), Option.Some("foo"))), true };
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.Some(3), Option.None<string>())), true };
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.None<int>(), Option.Some("foo"))), true };
            yield return new object[] { new ModuleLogOptions("id", LogsContentEncoding.None, LogsContentType.Json, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.None<int>(), Option.None<string>())), true };
        }
    }
}
