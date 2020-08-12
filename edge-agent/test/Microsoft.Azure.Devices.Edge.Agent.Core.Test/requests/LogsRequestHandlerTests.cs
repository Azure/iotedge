// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LogsRequestHandlerTests
    {
        [Fact]
        public async Task GetJsonLogsTest()
        {
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some("1501000"), Option.None<string>(), Option.Some(3), Option.Some("ERR"));
            LogsContentEncoding contentEncoding = LogsContentEncoding.None;
            LogsContentType contentType = LogsContentType.Json;

            string payload =
                @"{
                    ""schemaVersion"": ""1.0"",
                    ""items"": {
                        ""id"": ""m1"",
                        ""filter"": <filter>
                    },
                    ""encoding"": ""none"",
                    ""contentType"": ""json""
                }"
                    .Replace("<filter>", filter.ToJson());

            string iotHub = "foo.azure-devices.net";
            string deviceId = "d1";
            string mod1 = "m1";
            string mod2 = "m2";
            string mod3 = "m3";
            var moduleRuntimeInfoList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(mod1, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod2, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod3, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>())
            };
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(moduleRuntimeInfoList);

            var logsProvider = new Mock<ILogsProvider>();

            var module1LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var mod1Logs = new List<ModuleLogMessage>
            {
                new ModuleLogMessage(iotHub, deviceId, mod1, "0", 6, Option.None<DateTime>(), "log line 1"),
                new ModuleLogMessage(iotHub, deviceId, mod1, "0", 6, Option.None<DateTime>(), "log line 2"),
                new ModuleLogMessage(iotHub, deviceId, mod1, "0", 6, Option.None<DateTime>(), "log line 3")
            };

            logsProvider.Setup(l => l.GetLogs(mod1, module1LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod1Logs.ToBytes());

            // Act
            var logsRequestHandler = new ModuleLogsRequestHandler(logsProvider.Object, runtimeInfoProvider.Object);
            Option<string> response = await logsRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.True(response.HasValue);
            logsProvider.VerifyAll();
            runtimeInfoProvider.VerifyAll();
            var logsResponseList = response.OrDefault().FromJson<List<ModuleLogsResponse>>();
            Assert.NotNull(logsResponseList);
            Assert.Single(logsResponseList);
            ModuleLogsResponse logsResponse = logsResponseList[0];
            Assert.Equal(mod1, logsResponse.Id);
            Assert.True(logsResponse.Payload.HasValue);
            Assert.False(logsResponse.PayloadBytes.HasValue);
            Assert.Equal(mod1Logs.ToJson(), logsResponse.Payload.OrDefault());
        }

        [Fact]
        public async Task GetTextLogsTest()
        {
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some("1501000"), Option.None<string>(), Option.Some(3), Option.Some("ERR"));
            LogsContentEncoding contentEncoding = LogsContentEncoding.None;
            LogsContentType contentType = LogsContentType.Text;

            string payload =
                @"{
                    ""schemaVersion"": ""1.0"",
                    ""items"": {
                        ""id"": ""m1"",
                        ""filter"": <filter>
                    },
                    ""encoding"": ""none"",
                    ""contentType"": ""text""
                }"
                    .Replace("<filter>", filter.ToJson());

            string mod1 = "m1";
            string mod2 = "m2";
            string mod3 = "m3";
            var moduleRuntimeInfoList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(mod1, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod2, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod3, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>())
            };
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(moduleRuntimeInfoList);

            var logsProvider = new Mock<ILogsProvider>();

            var module1LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            string mod1Logs = new[]
            {
                "Log line 1\n",
                "Log line 2\n",
                "Log line 3\n"
            }.Join(string.Empty);

            logsProvider.Setup(l => l.GetLogs(mod1, module1LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod1Logs.ToBytes());

            // Act
            var logsRequestHandler = new ModuleLogsRequestHandler(logsProvider.Object, runtimeInfoProvider.Object);
            Option<string> response = await logsRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.True(response.HasValue);
            logsProvider.VerifyAll();
            runtimeInfoProvider.VerifyAll();
            var logsResponseList = response.OrDefault().FromJson<List<ModuleLogsResponse>>();
            Assert.NotNull(logsResponseList);
            Assert.Single(logsResponseList);
            ModuleLogsResponse logsResponse = logsResponseList[0];
            Assert.Equal(mod1, logsResponse.Id);
            Assert.True(logsResponse.Payload.HasValue);
            Assert.False(logsResponse.PayloadBytes.HasValue);
            Assert.Equal(mod1Logs, logsResponse.Payload.OrDefault());
        }

        [Fact]
        public async Task GetJsonGzipLogsTest()
        {
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some("1501000"), Option.None<string>(), Option.Some(3), Option.Some("ERR"));
            LogsContentEncoding contentEncoding = LogsContentEncoding.Gzip;
            LogsContentType contentType = LogsContentType.Json;

            string payload =
                @"{
                    ""schemaVersion"": ""1.0"",
                    ""items"": {
                        ""id"": ""m1"",
                        ""filter"": <filter>
                    },
                    ""encoding"": ""gzip"",
                    ""contentType"": ""json""
                }"
                    .Replace("<filter>", filter.ToJson());

            string iotHub = "foo.azure-devices.net";
            string deviceId = "d1";
            string mod1 = "m1";
            string mod2 = "m2";
            string mod3 = "m3";
            var moduleRuntimeInfoList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(mod1, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod2, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod3, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>())
            };
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(moduleRuntimeInfoList);

            var logsProvider = new Mock<ILogsProvider>();

            var module1LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var mod1Logs = new List<ModuleLogMessage>
            {
                new ModuleLogMessage(iotHub, deviceId, mod1, "0", 6, Option.None<DateTime>(), "log line 1"),
                new ModuleLogMessage(iotHub, deviceId, mod1, "0", 6, Option.None<DateTime>(), "log line 2"),
                new ModuleLogMessage(iotHub, deviceId, mod1, "0", 6, Option.None<DateTime>(), "log line 3")
            };
            byte[] mod1LogBytes = Compression.CompressToGzip(mod1Logs.ToBytes());
            logsProvider.Setup(l => l.GetLogs(mod1, module1LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod1LogBytes);

            // Act
            var logsRequestHandler = new ModuleLogsRequestHandler(logsProvider.Object, runtimeInfoProvider.Object);
            Option<string> response = await logsRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.True(response.HasValue);
            logsProvider.VerifyAll();
            runtimeInfoProvider.VerifyAll();
            var logsResponseList = response.OrDefault().FromJson<List<ModuleLogsResponse>>();
            Assert.NotNull(logsResponseList);
            Assert.Single(logsResponseList);
            ModuleLogsResponse logsResponse = logsResponseList[0];
            Assert.Equal(mod1, logsResponse.Id);
            Assert.False(logsResponse.Payload.HasValue);
            Assert.True(logsResponse.PayloadBytes.HasValue);
            Assert.Equal(mod1LogBytes, logsResponse.PayloadBytes.OrDefault());
        }

        [Fact]
        public async Task GetTextGzipLogsTest()
        {
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some("1501000"), Option.None<string>(), Option.Some(3), Option.Some("ERR"));
            LogsContentEncoding contentEncoding = LogsContentEncoding.Gzip;
            LogsContentType contentType = LogsContentType.Text;

            string payload =
                @"{
                    ""schemaVersion"": ""1.0"",
                    ""items"": {
                        ""id"": ""m1"",
                        ""filter"": <filter>
                    },
                    ""encoding"": ""gzip"",
                    ""contentType"": ""text""
                }"
                    .Replace("<filter>", filter.ToJson());

            string mod1 = "m1";
            string mod2 = "m2";
            string mod3 = "m3";
            var moduleRuntimeInfoList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(mod1, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod2, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>()),
                new ModuleRuntimeInfo(mod3, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>())
            };
            var runtimeInfoProvider = new Mock<IRuntimeInfoProvider>();
            runtimeInfoProvider.Setup(r => r.GetModules(It.IsAny<CancellationToken>()))
                .ReturnsAsync(moduleRuntimeInfoList);

            var logsProvider = new Mock<ILogsProvider>();

            var module1LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            string mod1Logs = new[]
            {
                "Log line 1\n",
                "Log line 2\n",
                "Log line 3\n"
            }.Join(string.Empty);
            byte[] mod1LogBytes = Compression.CompressToGzip(mod1Logs.ToBytes());
            logsProvider.Setup(l => l.GetLogs(mod1, module1LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod1LogBytes);

            // Act
            var logsRequestHandler = new ModuleLogsRequestHandler(logsProvider.Object, runtimeInfoProvider.Object);
            Option<string> response = await logsRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.True(response.HasValue);
            logsProvider.VerifyAll();
            runtimeInfoProvider.VerifyAll();
            var logsResponseList = response.OrDefault().FromJson<List<ModuleLogsResponse>>();
            Assert.NotNull(logsResponseList);
            Assert.Single(logsResponseList);
            ModuleLogsResponse logsResponse = logsResponseList[0];
            Assert.Equal(mod1, logsResponse.Id);
            Assert.False(logsResponse.Payload.HasValue);
            Assert.True(logsResponse.PayloadBytes.HasValue);
            Assert.Equal(mod1LogBytes, logsResponse.PayloadBytes.OrDefault());
        }

        [Fact]
        public void InvalidCtorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new ModuleLogsRequestHandler(null, Mock.Of<IRuntimeInfoProvider>()));

            Assert.Throws<ArgumentNullException>(() => new ModuleLogsRequestHandler(Mock.Of<ILogsProvider>(), null));
        }

        [Fact]
        public async Task InvalidInputsTest()
        {
            var logsRequestHandler = new ModuleLogsRequestHandler(Mock.Of<ILogsProvider>(), Mock.Of<IRuntimeInfoProvider>());
            await Assert.ThrowsAsync<ArgumentException>(() => logsRequestHandler.HandleRequest(Option.None<string>(), CancellationToken.None));

            string payload = @"{
                    ""items"": {
                        ""id"": ""m1"",
                        ""filter"": <filter>
                    },
                    ""encoding"": ""gzip"",
                    ""contentType"": ""text""
                }";
            await Assert.ThrowsAsync<ArgumentException>(() => logsRequestHandler.HandleRequest(Option.Some(payload), CancellationToken.None));
        }
    }
}
