// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class LogsProcessorTest
    {
        static readonly byte[] TestLogBytes = new byte[]
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

        [Fact]
        public async Task GetTextTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            var logMessageParser = new LogMessageParser(iotHub, deviceId);
            var logsProcessor = new LogsProcessor(logMessageParser);
            var stream = new MemoryStream(TestLogBytes);

            // Act
            IEnumerable<string> textLines = await logsProcessor.GetText(stream);

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

            // Act
            IEnumerable<ModuleLogMessage> logMessages = await logsProcessor.GetMessages(stream, moduleId);

            // Assert
            Assert.NotNull(logMessages);
            List<ModuleLogMessage> logMessagesList = logMessages.ToList();
            for (int i = 0; i < logMessagesList.Count; i++)
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
    }
}
