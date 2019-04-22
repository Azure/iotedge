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
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty), false };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.None<int>(), Option.None<string>())), false };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.Some(3), Option.Some("foo"))), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.Some(3), Option.None<string>())), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.None<int>(), Option.Some("foo"))), true };
            yield return new object[] { new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Json, new ModuleLogFilter(Option.Some(10), Option.Some(100), Option.None<int>(), Option.None<string>())), true };
        }

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

            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty);

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
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;
            string expectedLogText = TestLogTexts.Join(string.Empty);

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty);

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
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty);

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
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, false, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(DockerFraming.Frame(TestLogTexts)));

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty);

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
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            byte[] dockerLogsStreamBytes = DockerFraming.Frame(TestLogTexts);
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ModuleRuntimeInfo(moduleId, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()) });

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty);

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
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ModuleRuntimeInfo(moduleId, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()) });

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var filter = new ModuleLogFilter(tail, since, Option.Some(6), Option.Some("Starting"));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter);

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
                .Skip(8)
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
            Option<int> since = Option.Some(1552887267);
            CancellationToken cancellationToken = CancellationToken.None;

            string moduleId1 = "mod1";
            string moduleId2 = "mod2";

            var filter1 = new ModuleLogFilter(tail, since, Option.Some(6), Option.Some("Starting"));
            var filter2 = new ModuleLogFilter(Option.None<int>(), Option.None<int>(), Option.None<int>(), Option.Some("bad"));

            byte[] dockerLogsStreamBytes1 = DockerFraming.Frame(TestLogTexts);
            byte[] dockerLogsStreamBytes2 = DockerFraming.Frame(TestLogTexts);

            var modulesList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(moduleId1, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(moduleId2, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>())
            };

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId1, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes1));
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId2, true, Option.None<int>(), Option.None<int>(), cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes2));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modulesList);

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            var logOptions1 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter1);
            var logOptions2 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter2);
            var logIds = new List<(string, ModuleLogOptions)> { (moduleId1, logOptions1), (moduleId2, logOptions2) };

            var receivedBytes = new List<byte[]>();
            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.Add(bytes.ToArray());
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
                        .Skip(8)
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
            Option<int> since = Option.None<int>();
            CancellationToken cancellationToken = CancellationToken.None;

            string moduleId1 = "mod1";
            string moduleId2 = "mod2";

            var filter = new ModuleLogFilter(tail, since, Option.None<int>(), Option.Some("bad"));

            byte[] dockerLogsStreamBytes1 = DockerFraming.Frame(TestLogTexts);
            byte[] dockerLogsStreamBytes2 = DockerFraming.Frame(TestLogTexts);

            var modulesList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(moduleId1, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(moduleId2, "docker", ModuleStatus.Running, "foo", 0, Option.None<DateTime>(), Option.None<DateTime>())
            };

            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId1, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes1));
            runtimeInfoProvider.Setup(r => r.GetModuleLogs(moduleId2, true, tail, since, cancellationToken))
                .ReturnsAsync(new MemoryStream(dockerLogsStreamBytes2));
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modulesList);

            var logsProcessor = new LogsProcessor(new LogMessageParser(iotHub, deviceId));
            var logsProvider = new LogsProvider(runtimeInfoProvider.Object, logsProcessor);

            string regex = "mod";
            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter);

            var receivedBytes = new List<byte[]>();
            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.Add(bytes.ToArray());
                return Task.CompletedTask;
            }

            var expectedTextLines = new List<string> { TestLogTexts[3], TestLogTexts[4], TestLogTexts[3], TestLogTexts[4] };
            expectedTextLines.Sort();

            // Act
            await logsProvider.GetLogsStream(regex, logOptions, Callback, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Assert
            Assert.NotEmpty(receivedBytes);
            List<string> receivedText = receivedBytes
                .Select(
                    r =>
                        Compression.DecompressFromGzip(r)
                        .Skip(8)
                        .ToArray()
                        .FromBytes())
                .ToList();
            receivedText.Sort();

            Assert.Equal(expectedTextLines, receivedText);
        }

        [Theory]
        [MemberData(nameof(GetNeedToProcessStreamTestData))]
        public void NeedToProcessStreamTest(ModuleLogOptions logOptions, bool expectedResult)
        {
            Assert.Equal(expectedResult, LogsProvider.NeedToProcessStream(logOptions));
        }

        [Theory]
        [MemberData(nameof(GetMatchingIdsTestData))]
        public void GetMatchingIdsTest(string regex, IList<string> moduleIds, IList<string> expectedList)
        {
            ISet<string> actualModules = LogsProvider.GetMatchingIds(regex, moduleIds);
            Assert.Equal(expectedList.OrderBy(i => i), actualModules.OrderBy(i => i));
        }

        [Theory]
        [MemberData(nameof(GetIdsToProcessTestData))]
        public void GetIdsToProcessTest(IList<(string id, ModuleLogOptions logOptions)> idList, IList<string> allIds, IDictionary<string, ModuleLogOptions> expectedIdsToProcess)
        {
            IDictionary<string, ModuleLogOptions> idsToProcess = LogsProvider.GetIdsToProcess(idList, allIds);
            Assert.Equal(expectedIdsToProcess, idsToProcess);
        }

        public static IEnumerable<object[]> GetIdsToProcessTestData()
        {
            var logOptions1 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty);
            var logOptions2 = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty);
            var logOptions3 = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, new ModuleLogFilter(Option.Some(100), Option.None<int>(), Option.None<int>(), Option.None<string>()));

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("edgeAgent", logOptions1),
                    ("edgeHub", logOptions2),
                    ("tempSensor", logOptions3)
                },
                new List<string> { "edgeAgent", "edgeHub", "tempSensor", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions2,
                    ["tempSensor"] = logOptions3
                }
            };

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("edgeAgent", logOptions1),
                    ("edgeHub", logOptions2),
                    ("tempSensor", logOptions3)
                },
                new List<string> { "edgeAgent", "edgeHub", "tempSimulator", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions2
                }
            };

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("edge", logOptions1),
                    ("edgeHub", logOptions2),
                    ("e.*e", logOptions3)
                },
                new List<string> { "edgeAgent", "edgeHub", "module1", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions1,
                    ["eModule2"] = logOptions3
                }
            };

            yield return new object[]
            {
                new List<(string id, ModuleLogOptions logOptions)>
                {
                    ("^e.*", logOptions1),
                    ("mod", logOptions2)
                },
                new List<string> { "edgeAgent", "edgeHub", "module1", "eModule2" },
                new Dictionary<string, ModuleLogOptions>
                {
                    ["edgeAgent"] = logOptions1,
                    ["edgeHub"] = logOptions1,
                    ["eModule2"] = logOptions1,
                    ["module1"] = logOptions2,
                }
            };
        }

        public static IEnumerable<object[]> GetMatchingIdsTestData()
        {
            yield return new object[]
            {
                "edge",
                new List<string> { "edgehub", "edgeAgent", "module1", "edgMod2" },
                new List<string> { "edgehub", "edgeAgent" },
            };

            yield return new object[]
            {
                "e.*t",
                new List<string> { "edgehub", "edgeAgent", "module1", "eandt" },
                new List<string> { "edgeAgent", "eandt" },
            };

            yield return new object[]
            {
                "EDGE",
                new List<string> { "edgehub", "edgeAgent", "module1", "testmod3" },
                new List<string> { "edgehub", "edgeAgent" },
            };

            yield return new object[]
            {
                "^e.*",
                new List<string> { "edgehub", "edgeAgent", "module1", "eandt" },
                new List<string> { "edgehub", "edgeAgent", "eandt" },
            };
        }
    }
}
