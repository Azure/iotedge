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
        [Theory]
        [MemberData(nameof(GetLogsUploadRequestHandlerData))]
        public async Task TestLogsUploadRequest(string payload, string id, string sasUrl, LogsContentEncoding contentEncoding, LogsContentType contentType, ModuleLogFilter filter)
        {
            // Arrange
            var logsUploader = new Mock<ILogsUploader>();
            var logsProvider = new Mock<ILogsProvider>();
            var uploadBytes = new byte[100];
            var moduleLogOptions = new ModuleLogOptions(id, contentEncoding, contentType, filter);
            logsProvider.Setup(l => l.GetLogs(moduleLogOptions, It.IsAny<CancellationToken>()))
                .ReturnsAsync(uploadBytes);
            logsUploader.Setup(l => l.Upload(sasUrl, id, uploadBytes, contentEncoding, contentType))
                .Returns(Task.CompletedTask);

            // Act
            var logsUploadRequestHandler = new LogsUploadRequestHandler(logsUploader.Object, logsProvider.Object, Mock.Of<IRuntimeInfoProvider>());
            Option<string> response = await logsUploadRequestHandler.HandleRequest(Option.Maybe(payload), CancellationToken.None);

            // Assert
            Assert.False(response.HasValue);
        }

        //[Fact]
        //public async Task TestLogsUploadAllTaskRequest()
        //{
        //    // Arrange
        //    string sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
        //    var filter = new ModuleLogFilter(Option.Some(100), Option.Some(1501000), Option.Some(3), Option.Some("ERR"));
        //    LogsContentEncoding contentEncoding = LogsContentEncoding.None;
        //    LogsContentType contentType = LogsContentType.Json;
        //    string payload = @"{""id"": ""all"",  ""sasUrl"": ""<sasurl>""}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, contentEncoding, contentType, filter);
            


        //    var logsUploader = new Mock<ILogsUploader>();
        //    var logsProvider = new Mock<ILogsProvider>();
        //    var uploadBytes = new byte[100];
        //    var moduleLogOptions = new ModuleLogOptions(id, contentEncoding, contentType, filter);
        //    logsProvider.Setup(l => l.GetLogs(moduleLogOptions, It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(uploadBytes);
        //    logsUploader.Setup(l => l.Upload(sasUrl, id, uploadBytes, contentEncoding, contentType))
        //        .Returns(Task.CompletedTask);

        //    // Act
        //    var logsUploadRequestHandler = new LogsUploadRequestHandler(logsUploader.Object, logsProvider.Object, Mock.Of<IRuntimeInfoProvider>());
        //    Option<string> response = await logsUploadRequestHandler.HandleRequest(Option.Maybe(payload));

        //    // Assert
        //    Assert.False(response.HasValue);
        //}

        public static IEnumerable<object[]> GetLogsUploadRequestHandlerData()
        {
            string sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>""}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""encoding"": ""gzip""}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, LogsContentEncoding.Gzip, LogsContentType.Json, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""encoding"": ""gzip"", ""contentType"": ""text""}".Replace("<sasurl>", sasUrl), "mod1", sasUrl, LogsContentEncoding.Gzip, LogsContentType.Text, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""encoding"": ""none"", ""contentType"": ""json""}".Replace("<sasurl>", sasUrl), "edgeHub", sasUrl, LogsContentEncoding.None, LogsContentType.Json, ModuleLogFilter.Empty };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            var filter = new ModuleLogFilter(Option.Some(100), Option.Some(1501000), Option.Some(3), Option.Some("ERR"));
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""filter"": <filter>}".Replace("<sasurl>", sasUrl).Replace("<filter>", filter.ToJson()), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, filter };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            filter = new ModuleLogFilter(Option.None<int>(), Option.Some(1501000), Option.None<int>(), Option.Some("ERR"));
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""filter"": <filter>}".Replace("<sasurl>", sasUrl).Replace("<filter>", filter.ToJson()), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, filter };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            filter = new ModuleLogFilter(Option.Some(100), Option.None<int>(), Option.Some(3), Option.None<string>());
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""filter"": <filter>}".Replace("<sasurl>", sasUrl).Replace("<filter>", filter.ToJson()), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json, filter };
        }
    }
}
