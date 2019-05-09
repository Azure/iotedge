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
    public class LogsUploadRequestHandlerTest
    {
        public static IEnumerable<object[]> GetLogsUploadRequestHandlerData()
        {
            string sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""edgeAgent""}}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""edgeAgent""},""encoding"":""gzip""}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""mod1""},""encoding"":""gzip"",""contentType"":""text""}".Replace("<sasurl>", sasUrl), "mod1", sasUrl, LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""edgeHub""},""encoding"":""none"",""contentType"":""json""}".Replace("<sasurl>", sasUrl), "edgeHub", sasUrl, LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some(1501000), Option.Some(3), Option.Some("ERR"));
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""edgeAgent"",""filter"":<filter>}}".Replace("<sasurl>", sasUrl).Replace("<filter>", filter.ToJson()), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, filter };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            filter = new ModuleLogFilter(Option.None<int>(), Option.Some(1501000), Option.None<int>(), Option.Some("ERR"));
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""edgeAgent"",""filter"":<filter>}}".Replace("<sasurl>", sasUrl).Replace("<filter>", filter.ToJson()), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, filter };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            filter = new ModuleLogFilter(Option.Some(100), Option.None<int>(), Option.Some(3), Option.None<string>());
            yield return new object[] { @"{""schemaVersion"":""1.0"",""sasUrl"":""<sasurl>"",""items"":{""id"":""edgeAgent"",""filter"":<filter>}}".Replace("<sasurl>", sasUrl).Replace("<filter>", filter.ToJson()), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, filter };
        }

        [Theory]
        [MemberData(nameof(GetLogsUploadRequestHandlerData))]
        public async Task TestLogsUploadRequest(string payload, string id, string sasUrl, LogsContentEncoding contentEncoding, LogsContentType contentType, ModuleLogFilter filter)
        {
            // Arrange
            var logsUploader = new Mock<ILogsUploader>();
            var logsProvider = new Mock<ILogsProvider>();
            var uploadBytes = new byte[100];
            var moduleLogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);

            if (contentType == LogsContentType.Text)
            {
                Func<ArraySegment<byte>, Task> getLogsCallback = bytes => Task.CompletedTask;
                logsUploader.Setup(l => l.GetUploaderCallback(sasUrl, id, contentEncoding, contentType))
                    .ReturnsAsync(getLogsCallback);
                logsProvider.Setup(l => l.GetLogsStream(id, moduleLogOptions, getLogsCallback, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
            }
            else
            {
                logsProvider.Setup(l => l.GetLogs(id, moduleLogOptions, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(uploadBytes);
                logsUploader.Setup(l => l.Upload(sasUrl, id, uploadBytes, contentEncoding, contentType))
                    .Returns(Task.CompletedTask);
            }

            IEnumerable<ModuleRuntimeInfo> moduleRuntimeInfoList = new List<ModuleRuntimeInfo>
            {
                new ModuleRuntimeInfo(id, "docker", ModuleStatus.Running, string.Empty, 0, Option.None<DateTime>(), Option.None<DateTime>())
            };
            var runtimeInfoProvider = Mock.Of<IRuntimeInfoProvider>(r => r.GetModules(It.IsAny<CancellationToken>()) == Task.FromResult(moduleRuntimeInfoList));

            // Act
            var logsUploadRequestHandler = new LogsUploadRequestHandler(logsUploader.Object, logsProvider.Object, runtimeInfoProvider);
            Option<string> response = await logsUploadRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.False(response.HasValue);
            logsProvider.VerifyAll();
            logsUploader.VerifyAll();
            Mock.Get(runtimeInfoProvider).VerifyAll();
        }

        [Fact]
        public async Task TestLogsUploadAllTaskRequest()
        {
            // Arrange
            string sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some(1501000), Option.Some(3), Option.Some("ERR"));
            LogsContentEncoding contentEncoding = LogsContentEncoding.None;
            LogsContentType contentType = LogsContentType.Json;

            string payload =
                @"{
                    ""schemaVersion"": ""1.0"",
                    ""sasUrl"": ""<sasurl>"",
                    ""items"": {
                        ""id"": "".*"",
                        ""filter"": <filter>
                    },
                    ""encoding"": ""none"",
                    ""contentType"": ""json""
                }"
                    .Replace("<sasurl>", sasUrl)
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

            var logsUploader = new Mock<ILogsUploader>();
            var logsProvider = new Mock<ILogsProvider>();

            var module1LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var mod1LogBytes = new byte[100];
            logsProvider.Setup(l => l.GetLogs(mod1, module1LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod1LogBytes);
            logsUploader.Setup(l => l.Upload(sasUrl, mod1, mod1LogBytes, contentEncoding, contentType))
                .Returns(Task.CompletedTask);

            var module2LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var mod2LogBytes = new byte[80];
            logsProvider.Setup(l => l.GetLogs(mod2, module2LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod2LogBytes);
            logsUploader.Setup(l => l.Upload(sasUrl, mod2, mod2LogBytes, contentEncoding, contentType))
                .Returns(Task.CompletedTask);

            var module3LogOptions = new ModuleLogOptions(contentEncoding, contentType, filter, LogOutputFraming.None, Option.None<LogsOutputGroupingConfig>(), false);
            var mod3LogBytes = new byte[120];
            logsProvider.Setup(l => l.GetLogs(mod3, module3LogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mod3LogBytes);
            logsUploader.Setup(l => l.Upload(sasUrl, mod3, mod3LogBytes, contentEncoding, contentType))
                .Returns(Task.CompletedTask);

            // Act
            var logsUploadRequestHandler = new LogsUploadRequestHandler(logsUploader.Object, logsProvider.Object, runtimeInfoProvider.Object);
            Option<string> response = await logsUploadRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.False(response.HasValue);
            logsProvider.VerifyAll();
            logsUploader.VerifyAll();
            runtimeInfoProvider.VerifyAll();
        }
    }
}
