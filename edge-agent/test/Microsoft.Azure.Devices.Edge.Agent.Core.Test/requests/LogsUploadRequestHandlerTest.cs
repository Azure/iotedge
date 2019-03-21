// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LogsUploadRequestHandlerTest
    {
        [Theory]
        [MemberData(nameof(GetLogsUploadRequestHandlerData))]
        public async Task TestLogsUploadRequest(string payload, string id, string sasUrl, LogsContentEncoding contentEncoding, LogsContentType contentType)
        {
            // Arrange
            var logsUploader = new Mock<ILogsUploader>();
            var logsProvider = new Mock<ILogsProvider>();
            var uploadBytes = new byte[100];
            logsProvider.Setup(l => l.GetLogs(It.IsAny<ModuleLogOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(uploadBytes);
            logsUploader.Setup(l => l.Upload(sasUrl, id, uploadBytes, contentEncoding, contentType))
                .Returns(Task.CompletedTask);

            // Act
            var logsUploadRequestHandler = new LogsUploadRequestHandler(logsUploader.Object, logsProvider.Object);
            Option<string> response = await logsUploadRequestHandler.HandleRequest(Option.Maybe(payload));

            // Assert
            Assert.False(response.HasValue);
        }

        public static IEnumerable<object[]> GetLogsUploadRequestHandlerData()
        {
            string sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>""}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, LogsContentEncoding.None, LogsContentType.Json };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""encoding"": ""gzip""}".Replace("<sasurl>", sasUrl), "edgeAgent", sasUrl, LogsContentEncoding.Gzip, LogsContentType.Json };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""encoding"": ""gzip"", ""contentType"": ""text""}".Replace("<sasurl>", sasUrl), "mod1", sasUrl, LogsContentEncoding.Gzip, LogsContentType.Text };

            sasUrl = $"https://test1.blob.core.windows.net/cont2?st={Guid.NewGuid()}";
            yield return new object[] { @"{""id"": ""edgeAgent"",  ""sasUrl"": ""<sasurl>"", ""encoding"": ""none"", ""contentType"": ""json""}".Replace("<sasurl>", sasUrl), "edgeHub", sasUrl, LogsContentEncoding.None, LogsContentType.Json };
        }
    }
}
