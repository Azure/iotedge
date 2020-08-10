// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class LogsProcessorTest
    {
        static readonly byte[] TestLogBytes =
        {
            1, 0, 0, 0, 0, 0, 0, 43, 91, 50, 48, 49, 57, 45, 48, 50, 45, 48, 56, 32, 48, 50, 58, 50, 51, 58, 50, 50, 32, 58, 32, 83, 116, 97, 114, 116, 105, 110, 103, 32, 69, 100, 103, 101, 32, 65, 103, 101, 110, 116, 10,
            1, 0, 0, 0, 0, 0, 0, 47, 91, 48, 50, 47, 48, 56, 47, 50, 48, 49, 57, 32, 48, 50, 58, 50, 51, 58, 50, 50, 46, 57, 48, 55, 32, 65, 77, 93, 32, 69, 100, 103, 101, 32, 65, 103, 101, 110, 116, 32, 77, 97, 105, 110, 40, 41, 10,
            1, 0, 0, 0, 0, 0, 0, 73, 50, 48, 49, 57, 45, 48, 50, 45, 48, 56, 32, 48, 50, 58, 50, 51, 58, 50, 51, 46, 49, 51, 55, 32, 43, 48, 48, 58, 48, 48, 32, 91, 73, 78, 70, 93, 32, 45, 32, 83, 116, 97, 114, 116, 105, 110, 103, 32, 109, 111, 100, 117, 108, 101, 32, 109, 97, 110, 97, 103, 101, 109, 101, 110, 116, 32, 97, 103, 101, 110, 116, 46, 10,
            1, 0, 0, 0, 0, 0, 0, 111, 50, 48, 49, 57, 45, 48, 50, 45, 48, 56, 32, 48, 50, 58, 50, 51, 58, 50, 51, 46, 51, 50, 48, 32, 43, 48, 48, 58, 48, 48, 32, 91, 73, 78, 70, 93, 32, 45, 32, 86, 101, 114, 115, 105, 111, 110, 32, 45, 32, 49, 46, 48, 46, 55, 45, 100, 101, 118, 46, 50, 48, 48, 53, 53, 49, 54, 50, 32, 40, 57, 99, 49, 54, 52, 102, 48, 102, 97, 49, 98, 54, 97, 99, 99, 98, 52, 55, 100, 98, 50, 56, 55, 52, 98, 100, 49, 100, 102, 57, 48, 102, 48, 48, 99, 53, 98, 102, 51, 56, 41, 10,
            1, 0, 0, 0, 0, 0, 0, 40, 50, 48, 49, 57, 45, 48, 50, 45, 48, 56, 32, 48, 50, 58, 50, 51, 58, 50, 51, 46, 51, 50, 49, 32, 43, 48, 48, 58, 48, 48, 32, 91, 73, 78, 70, 93, 32, 45, 32, 10,
            1, 0, 0, 0, 0, 0, 0, 119, 32, 32, 32, 32, 32, 32, 32, 32, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 149, 151, 32, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 149, 151, 226, 150, 136, 226, 150, 136, 226, 149, 151, 32, 32, 32, 226, 150, 136, 226, 150, 136, 226, 149, 151, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 149, 151, 32, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 150, 136, 226, 149, 151, 10
        };

        static readonly List<(string rawText, string parsedText, string timestamp)> TestLogLines = new List<(string, string, string)>
        {
            ("[2019-02-08 02:23:22 : Starting Edge Agent\n", "[2019-02-08 02:23:22 : Starting Edge Agent", string.Empty),
            ("[02/08/2019 02:23:22.907 AM] Edge Agent Main()\n", "[02/08/2019 02:23:22.907 AM] Edge Agent Main()", string.Empty),
            ("2019-02-08 02:23:23.137 +00:00 [INF] - Starting module management agent.\n", "[INF] - Starting module management agent.", "2019-02-08 02:23:23.137 +00:00"),
            ("2019-02-08 02:23:23.320 +00:00 [INF] - Version - 1.0.7-dev.20055162 (9c164f0fa1b6accb47db2874bd1df90f00c5bf38)\n", "[INF] - Version - 1.0.7-dev.20055162 (9c164f0fa1b6accb47db2874bd1df90f00c5bf38)", "2019-02-08 02:23:23.320 +00:00"),
            ("2019-02-08 02:23:23.321 +00:00 [INF] - \n", "[INF] - ", "2019-02-08 02:23:23.321 +00:00"),
            ("        █████╗ ███████╗██╗   ██╗██████╗ ███████╗\n", "█████╗ ███████╗██╗   ██╗██████╗ ███████╗", string.Empty)
        };

        static readonly string[] TestLogTexts =
        {
            "<6> 2019-02-08 02:23:23.137 +00:00 [INF] - Starting an important module.\n",
            "<7> 2019-02-09 02:23:23.137 +00:00 [DBG] - Some debug log entry.\n",
            "<6> 2019-02-10 02:23:23.137 +00:00 [INF] - Routine log line.\n",
            "<4> 2019-03-08 02:23:23.137 +00:00 [WRN] - Warning, something bad happened.\n",
            "<3> 2019-05-08 02:23:23.137 +00:00 [ERR] - Something really bad happened.\n"
        };

        public static IEnumerable<object[]> GetLogLevelFilterTestData()
        {
            yield return new object[] { 6, new List<int> { 0, 2 } };
            yield return new object[] { 7, new List<int> { 1 } };
            yield return new object[] { 4, new List<int> { 3 } };
            yield return new object[] { 3, new List<int> { 4 } };
        }

        public static IEnumerable<object[]> GetMultipleFiltersTestData()
        {
            yield return new object[] { 6, "important", new List<int> { 0 } };
            yield return new object[] { 7, "log", new List<int> { 1 } };
            yield return new object[] { 4, "bad", new List<int> { 3 } };
            yield return new object[] { 3, "bad", new List<int> { 4 } };
        }

        [Fact]
        public async Task GetTextTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(TestLogBytes);
            var filter = ModuleLogFilter.Empty;

            // Act
            IEnumerable<string> textLines = await logsProcessor.GetText(moduleId, stream, filter);

            // Assert
            Assert.NotNull(textLines);
            Assert.Equal(TestLogLines.Select(l => l.rawText), textLines);
        }

        [Fact]
        public async Task GetMessagesTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(TestLogBytes);
            var filter = ModuleLogFilter.Empty;

            // Act
            IEnumerable<ModuleLogMessage> logMessages = await logsProcessor.GetMessages(moduleId, stream, filter);

            // Assert
            Assert.NotNull(logMessages);
            List<ModuleLogMessage> logMessagesList = logMessages.ToList();
            Assert.Equal(TestLogLines.Count, logMessagesList.Count);
            for (int i = 0; i < TestLogLines.Count; i++)
            {
                ModuleLogMessage logMessage = logMessagesList[i];
                Assert.Equal(iotHub, logMessage.IoTHub);
                Assert.Equal(deviceId, logMessage.DeviceId);
                Assert.Equal(moduleId, logMessage.ModuleId);
                Assert.Equal(6, logMessage.LogLevel);
                Assert.Equal(TestLogLines[i].parsedText, logMessage.Text);
                Assert.Equal(!string.IsNullOrEmpty(TestLogLines[i].timestamp), logMessage.TimeStamp.HasValue);
                if (logMessage.TimeStamp.HasValue)
                {
                    Assert.Equal(DateTime.Parse(TestLogLines[i].timestamp), logMessage.TimeStamp.OrDefault());
                }
            }
        }

        [Fact]
        public async Task GetTextWithRegexFilterTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            string regex = @"\[INF\]";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(TestLogBytes);
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.Some(regex));

            // Act
            IEnumerable<string> textLines = await logsProcessor.GetText(moduleId, stream, filter);

            // Assert
            Assert.NotNull(textLines);
            Assert.Equal(3, textLines.Count());
            Assert.Equal(TestLogLines.Skip(2).Take(3).Select(l => l.rawText), textLines);
        }

        [Fact]
        public async Task GetMessagesWithRegexFilterTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            string regex = @"\[INF\]";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(TestLogBytes);
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.Some(regex));

            // Act
            IEnumerable<ModuleLogMessage> logMessages = await logsProcessor.GetMessages(moduleId, stream, filter);

            // Assert
            Assert.NotNull(logMessages);
            List<ModuleLogMessage> logMessagesList = logMessages.ToList();
            Assert.Equal(3, logMessagesList.Count);
            for (int i = 0; i < logMessagesList.Count; i++)
            {
                ModuleLogMessage logMessage = logMessagesList[i];
                (string rawText, string parsedText, string timestamp) expectedData = TestLogLines[i + 2];
                Assert.Equal(iotHub, logMessage.IoTHub);
                Assert.Equal(deviceId, logMessage.DeviceId);
                Assert.Equal(moduleId, logMessage.ModuleId);
                Assert.Equal(6, logMessage.LogLevel);
                Assert.Equal(expectedData.parsedText, logMessage.Text);
                Assert.Equal(!string.IsNullOrEmpty(expectedData.timestamp), logMessage.TimeStamp.HasValue);
                if (logMessage.TimeStamp.HasValue)
                {
                    Assert.Equal(DateTime.Parse(expectedData.timestamp), logMessage.TimeStamp.OrDefault());
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetLogLevelFilterTestData))]
        public async Task GetTextWithLogLevelFilterTest(int logLevel, List<int> expectedLogLineIds)
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.None<string>());

            // Act
            List<string> textLines = (await logsProcessor.GetText(moduleId, stream, filter)).ToList();

            // Assert
            Assert.NotNull(textLines);
            Assert.Equal(expectedLogLineIds.Count, textLines.Count);
            for (int i = 0; i < expectedLogLineIds.Count; i++)
            {
                Assert.Equal(TestLogTexts[expectedLogLineIds[i]], textLines[i]);
            }
        }

        [Theory]
        [MemberData(nameof(GetLogLevelFilterTestData))]
        public async Task GetMessagesWithLogLevelFilterTest(int logLevel, List<int> expectedLogLineIds)
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.None<string>());

            // Act
            IEnumerable<ModuleLogMessage> logMessages = await logsProcessor.GetMessages(moduleId, stream, filter);

            // Assert
            Assert.NotNull(logMessages);
            List<ModuleLogMessage> logMessagesList = logMessages.ToList();
            Assert.Equal(expectedLogLineIds.Count, logMessagesList.Count);
            for (int i = 0; i < logMessagesList.Count; i++)
            {
                ModuleLogMessage logMessage = logMessagesList[i];
                string expectedText = TestLogTexts[expectedLogLineIds[i]];
                Assert.Contains(logMessage.Text, expectedText, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [MemberData(nameof(GetMultipleFiltersTestData))]
        public async Task GetTextWithMultipleFiltersTest(int logLevel, string regex, List<int> expectedLogLineIds)
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));

            // Act
            List<string> textLines = (await logsProcessor.GetText(moduleId, stream, filter)).ToList();

            // Assert
            Assert.NotNull(textLines);
            Assert.Equal(expectedLogLineIds.Count, textLines.Count);
            for (int i = 0; i < expectedLogLineIds.Count; i++)
            {
                Assert.Equal(TestLogTexts[expectedLogLineIds[i]], textLines[i]);
            }
        }

        [Theory]
        [MemberData(nameof(GetMultipleFiltersTestData))]
        public async Task GetMessagesWithMultipleFiltersTest(int logLevel, string regex, List<int> expectedLogLineIds)
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));

            // Act
            IEnumerable<ModuleLogMessage> logMessages = await logsProcessor.GetMessages(moduleId, stream, filter);

            // Assert
            Assert.NotNull(logMessages);
            List<ModuleLogMessage> logMessagesList = logMessages.ToList();
            Assert.Equal(expectedLogLineIds.Count, logMessagesList.Count);
            for (int i = 0; i < logMessagesList.Count; i++)
            {
                ModuleLogMessage logMessage = logMessagesList[i];
                string expectedText = TestLogTexts[expectedLogLineIds[i]];
                Assert.Contains(logMessage.Text, expectedText, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task ProcessStreamTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            int logLevel = 6;
            string regex = "Starting";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream, logOptions, Callback);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotEmpty(receivedBytes);
            string receivedText = receivedBytes
                .ToArray()
                .FromBytes();
            Assert.Equal(TestLogTexts[0], receivedText);
        }

        [Fact]
        public async Task ProcessStreamToMessageTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            int logLevel = 6;
            string regex = "Starting";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Json, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream, logOptions, Callback);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotEmpty(receivedBytes);
            var logMessage = receivedBytes.ToArray().FromBytes<ModuleLogMessage>();
            Assert.Equal(iotHub, logMessage.IoTHub);
            Assert.Equal(deviceId, logMessage.DeviceId);
            Assert.Equal(moduleId, logMessage.ModuleId);
            Assert.Equal(6, logMessage.LogLevel);
            Assert.Contains(logMessage.Text, TestLogTexts[0]);
        }

        [Fact]
        public async Task ProcessStreamWithGzipTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            int logLevel = 6;
            string regex = "Starting";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream, logOptions, Callback);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotEmpty(receivedBytes);
            string receivedText = Compression.DecompressFromGzip(receivedBytes.ToArray())
                .ToArray()
                .FromBytes();
            Assert.Equal(TestLogTexts[0], receivedText);
        }

        [Fact]
        public async Task ProcessStreamWithSimpleFramingTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            int logLevel = 6;
            string regex = "Starting";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, filter, LogOutputFraming.SimpleLength, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream, logOptions, Callback);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotEmpty(receivedBytes);
            IList<string> receivedChunks = SimpleFraming.Parse(receivedBytes.ToArray())
                .Select(r => Encoding.UTF8.GetString(r))
                .ToList();
            Assert.Equal(1, receivedChunks.Count);
        }

        [Fact]
        public async Task ProcessStreamToMessageWithSimpleFramingTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            int logLevel = 6;
            string regex = "Starting";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.Some(logLevel), Option.Some(regex));
            var logOptions = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Json, filter, LogOutputFraming.SimpleLength, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes = new List<byte>();

            Task Callback(ArraySegment<byte> bytes)
            {
                receivedBytes.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream, logOptions, Callback);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotEmpty(receivedBytes);
            IList<ModuleLogMessage> receivedChunks = SimpleFraming.Parse(receivedBytes.ToArray())
                .Select(r => r.FromBytes<ModuleLogMessage>())
                .ToList();
            Assert.Equal(1, receivedChunks.Count);
            var logMessage = receivedChunks[0];
            Assert.Equal(iotHub, logMessage.IoTHub);
            Assert.Equal(deviceId, logMessage.DeviceId);
            Assert.Equal(moduleId, logMessage.ModuleId);
            Assert.Equal(6, logMessage.LogLevel);
            Assert.Contains(logMessage.Text, TestLogTexts[0]);
        }

        [Fact]
        public async Task ProcessStreamWithGroupingGzipTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var filter = new ModuleLogFilter(Option.None<int>(), Option.None<string>(), Option.None<string>(), Option.None<int>(), Option.None<string>());

            var stream1 = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var logOptions1 = new ModuleLogOptions(LogsContentEncoding.None, LogsContentType.Text, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes1 = new List<byte>();

            Task Callback1(ArraySegment<byte> bytes)
            {
                receivedBytes1.AddRange(bytes.ToArray());
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream1, logOptions1, Callback1);

            // Assert
            Assert.NotEmpty(receivedBytes1);
            string receivedText1 = receivedBytes1.ToArray()
                .FromBytes();
            Assert.Equal(string.Join(string.Empty, TestLogTexts), receivedText1);

            // Arrange
            var stream2 = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var logOptions2 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            var receivedBytes2 = new List<ArraySegment<byte>>();

            Task Callback2(ArraySegment<byte> bytes)
            {
                receivedBytes2.Add(bytes);
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream2, logOptions2, Callback2);

            // Assert
            Assert.NotEmpty(receivedBytes2);
            IEnumerable<string> receivedText2 = receivedBytes2
                .Select(r => Compression.DecompressFromGzip(r.ToArray()))
                .Select(r => r.FromBytes());
            Assert.Equal(TestLogTexts, receivedText2);

            // Arrange
            var stream3 = new MemoryStream(DockerFraming.Frame(TestLogTexts));
            var logOptions3 = new ModuleLogOptions(LogsContentEncoding.Gzip, LogsContentType.Text, filter, LogOutputFraming.None, Option.Some(new LogsOutputGroupingConfig(100, TimeSpan.FromSeconds(10))), false);

            var receivedBytes3 = new List<ArraySegment<byte>>();

            Task Callback3(ArraySegment<byte> bytes)
            {
                receivedBytes3.Add(bytes);
                return Task.CompletedTask;
            }

            // Act
            await logsProcessor.ProcessLogsStream(moduleId, stream3, logOptions3, Callback3);

            // Assert
            Assert.NotEmpty(receivedBytes3);
            IEnumerable<string> receivedText3 = receivedBytes3
                .Select(r => Compression.DecompressFromGzip(r.ToArray()))
                .Select(r => r.FromBytes());
            Assert.Equal(string.Join(string.Empty, TestLogTexts), receivedText3.First());

            int countBytes2 = receivedBytes2.Select(r => r.Count).Sum();
            int countBytes3 = receivedBytes3.Select(r => r.Count).Sum();
            Assert.True(countBytes2 > countBytes3);
        }
    }
}
