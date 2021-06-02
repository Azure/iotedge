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

        public static IEnumerable<object[]> GetNeedToProcessStreamTestData()
        {
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), false };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.None<int>(), Option.None<string>()), LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), false };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.Some(3), Option.Some("foo")), LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.Some(3), Option.None<string>()), LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.None<int>(), Option.Some("foo")), LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Json, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.None<int>(), Option.None<string>()), LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.None<int>(), Option.None<string>()), LogOutputFraming.SimpleLength, Option.None<LogsOutputGroupingConfig>(), false), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some("100"), Option.None<string>(), Option.None<int>(), Option.None<string>()), LogOutputFraming.None, Option.Some(new LogsOutputGroupingConfig(100, TimeSpan.FromMilliseconds(1000))), false), false };
        }

        [Fact]
        public async Task GetLogsAsTextTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.None<int>();
            Option<string> since = Option.None<string>();
            CancellationToken cancellationToken = CancellationToken.None;
            string expectedLogText = TestLogTexts.Join(string.Empty);

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            // Act
            byte[] bytes = await logsProvider.GetLogs(moduleId, logOptions, cancellationToken);

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
            Option<string> since = Option.None<string>();
            CancellationToken cancellationToken = CancellationToken.None;
            string expectedLogText = TestLogTexts.Join(string.Empty);

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            // Act
            byte[] bytes = await logsProvider.GetLogs(moduleId, logOptions, cancellationToken);

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
            Option<string> since = Option.None<string>();
            CancellationToken cancellationToken = CancellationToken.None;

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            // Act
            byte[] bytes = await logsProvider.GetLogs(moduleId, logOptions, cancellationToken);

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
            Option<string> since = Option.None<string>();
            CancellationToken cancellationToken = CancellationToken.None;

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            // Act
            byte[] bytes = await logsProvider.GetLogs(moduleId, logOptions, cancellationToken);

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
            Option<string> since = Option.None<string>();
            CancellationToken cancellationToken = CancellationToken.None;

            byte[] dockerLogsStreamBytes = DockerFraming.Frame(TestLogTexts);
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, true, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ModuleRuntimeInfo(moduleId, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()) });

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), true);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProvider.GetLogsStream(moduleId, logOptions, Callback, cancellationToken);

            // Assert
            Assert.NotEmpty(receivedBytes);
            Assert.Equal(string.Join(string.Empty, TestLogTexts).ToBytes(), receivedBytes);
        }

        [Fact]
        public async Task GetLogsStreamWithFiltersTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            Option<int> tail = Option.Some(10);
            Option<string> since = Option.Some("1552887267");
            CancellationToken cancellationToken = CancellationToken.None;

            byte[] dockerLogsStreamBytes = DockerFraming.Frame(TestLogTexts);
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, true, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ModuleRuntimeInfo(moduleId, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()) });

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var filter = new ModuleLogFilter(tail, since, Option.None<string>(), Option.Some(6), Option.Some("Starting"));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), true);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProvider.GetLogsStream(moduleId, logOptions, Callback, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert
            Assert.NotEmpty(receivedBytes);
            string receivedText = Compression.DecompressFromGzip(receivedBytes.ToArray())
                .ToArray()
                .FromBytes();
            Assert.Equal(TestLogTexts[0], receivedText);
        }

        [Fact]
        public async Task GetLogsStreamWithMultipleModulesTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            Option<int> tail = Option.Some(10);
            Option<string> since = Option.Some("1552887267");
            CancellationToken cancellationToken = CancellationToken.None;

            string moduleId1 = "mod1";
            string moduleId2 = "mod2";

            var filter1 = new ModuleLogFilter(tail, since, Option.None<string>(), Option.Some(6), Option.Some("Starting"));
            var filter2 = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.Some("bad"));

            byte[] dockerLogsStreamBytes1 = DockerFraming.Frame(TestLogTexts);
            byte[] dockerLogsStreamBytes2 = DockerFraming.Frame(TestLogTexts);

            var modulesList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(moduleId1, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(moduleId2, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>())
            };

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId1, true, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes1));
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId2, true, Option.None<int>(), Option.None<string>(), Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes2));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modulesList);

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions1 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter1, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), true);
            var logOptions2 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter2, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), true);
            var logIds = new List<(string, ModuleLogOptions)> { (moduleId1, logOptions1), (moduleId2, logOptions2) };

            var receivedBytes = new List<byte[]>();

            Task Callback(ArraySegment<byte> bytes)
            {
                lock (receivedBytes)
                {
                    receivedBytes.Add(bytes.ToArray());
                }

                return Task.CompletedTask;
            }

            var expectedTextLines = new List<string> { TestLogTexts[0], TestLogTexts[3], TestLogTexts[4] };
            expectedTextLines.Sort();

            // Act
            await logsProvider.GetLogsStream(logIds, Callback, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Assert
            Assert.NotEmpty(receivedBytes);
            List<string> receivedText = receivedBytes
                .Select(
                    r =>
                        Compression.DecompressFromGzip(r)
                            .ToArray()
                            .FromBytes())
                .ToList();
            receivedText.Sort();

            Assert.Equal(expectedTextLines, receivedText);
        }

        [Fact]
        public async Task GetLogsStreamWithMultipleModulesWithRegexMatchTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            Option<int> tail = Option.None<int>();
            Option<string> since = Option.None<string>();
            CancellationToken cancellationToken = CancellationToken.None;

            string moduleId1 = "mod1";
            string moduleId2 = "mod2";

            var filter = new ModuleLogFilter(tail, since, Option.None<string>(), Option.None<int>(), Option.Some("bad"));

            byte[] dockerLogsStreamBytes1 = DockerFraming.Frame(TestLogTexts);
            byte[] dockerLogsStreamBytes2 = DockerFraming.Frame(TestLogTexts);

            var modulesList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(moduleId1, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(moduleId2, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>())
            };

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId1, true, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes1));
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId2, true, tail, since, Option.None<string>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes2));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modulesList);

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), true);
            var listOptions = new List<(string id, ModuleLogOptions logOptions)>
            {
                (moduleId1, logOptions),
                (moduleId2, logOptions)
            };

            var receivedBytes = new List<byte[]>();

            Task Callback(ArraySegment<byte> bytes)
            {
                lock (receivedBytes)
                {
                    receivedBytes.Add(bytes.ToArray());
                }

                return Task.CompletedTask;
            }

            var expectedTextLines = new List<string> { TestLogTexts[3], TestLogTexts[4], TestLogTexts[3], TestLogTexts[4] };
            expectedTextLines.Sort();

            // Act
            await logsProvider.GetLogsStream(listOptions, Callback, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Assert
            Assert.NotEmpty(receivedBytes);
            List<string> receivedText = receivedBytes
                .Select(
                    r =>
                        Compression.DecompressFromGzip(r)
                            .ToArray()
                            .FromBytes())
                .ToList();
            receivedText.Sort();

            Assert.Equal(expectedTextLines, receivedText);
        }
    }
}
