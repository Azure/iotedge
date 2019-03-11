// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using Akka.IO;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class LogMessageParserTest
    {
        [Theory]
        [InlineData(1, "stdout")]
        [InlineData(2, "stderr")]
        public void TestGetStream(byte streamByte, string stream)
        {
            Assert.Equal(stream, LogMessageParser.GetStream(streamByte));
        }

        [Theory]
        [InlineData("<3> 2019-02-14 16:15:35.243 -08:00 [INF] [EdgeHub] - LogLine", 3, "2019-02-14 16:15:35.243 -08:00", "[INF] [EdgeHub] - LogLine")]
        [InlineData("<6> 2019-02-14 16:15:35.243 -08:00 [INF] [EdgeHub] - LogLine", 6, "2019-02-14 16:15:35.243 -08:00", "[INF] [EdgeHub] - LogLine")]
        [InlineData(" 2019-02-14 16:15:35.243 -08:00 [INF] [EdgeHub] - LogLine", 6, "2019-02-14 16:15:35.243 -08:00", "[INF] [EdgeHub] - LogLine")]
        [InlineData("<6> 2019-02-14 16:15:35.243 [INF] [EdgeHub] - LogLine", 6, "", "2019-02-14 16:15:35.243 [INF] [EdgeHub] - LogLine")]
        [InlineData("<6> 2019-02-14 16:15:35.243 -08:00 ", 6, "2019-02-14 16:15:35.243 -08:00", "<6> 2019-02-14 16:15:35.243 -08:00 ")]
        [InlineData("[INF] [EdgeHub] - LogLine", 6, "", "[INF] [EdgeHub] - LogLine")]
        [InlineData("2 2019-02-14 16:15:35 [INF] [EdgeHub] - LogLine", 6, "", "2 2019-02-14 16:15:35 [INF] [EdgeHub] - LogLine")]
        public void TestParseLogText(string value, int expectedLogLevel, string expectedDateTime, string expectedText)
        {
            // Act
            (int logLevel, Option<DateTime> timeStamp, string logText) = LogMessageParser.ParseLogText(value);

            // Assert
            Assert.Equal(expectedText, logText);
            Assert.Equal(expectedLogLevel, logLevel);
            Assert.Equal(timeStamp.HasValue, !string.IsNullOrWhiteSpace(expectedDateTime));
            if (timeStamp.HasValue)
            {
                Assert.Equal(DateTime.Parse(expectedDateTime), timeStamp.OrDefault());
            }
        }

        [Fact]
        public void GetLogMessageTest()
        {
            // Arrange
            var frame = new byte[]
            {
                1, 0, 0, 0, 0, 0, 0, 43, 91, 50, 48, 49, 57, 45, 48, 50, 45, 48, 56, 32, 48, 50, 58, 50, 51, 58, 50, 50, 32, 58, 32, 83, 116, 97, 114, 116, 105, 110, 103, 32, 69, 100, 103, 101, 32, 65, 103, 101, 110, 116, 10
            };
            var byteString = ByteString.FromBytes(frame);
            string iotHub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            string expectedText = "[2019-02-08 02:23:22 : Starting Edge Agent";
            string expectedStream = "stdout";
            int expectedLogLevel = 6;

            // Act
            ModuleLogMessage moduleLogMessage = LogMessageParser.GetLogMessage(byteString, iotHub, deviceId, moduleId);

            // Assert
            Assert.NotNull(moduleLogMessage);
            Assert.Equal(expectedText, moduleLogMessage.Text);
            Assert.Equal(iotHub, moduleLogMessage.IoTHub);
            Assert.Equal(deviceId, moduleLogMessage.DeviceId);
            Assert.Equal(moduleId, moduleLogMessage.ModuleId);
            Assert.Equal(expectedStream, moduleLogMessage.Stream);
            Assert.Equal(expectedLogLevel, moduleLogMessage.LogLevel);
        }
    }
}
