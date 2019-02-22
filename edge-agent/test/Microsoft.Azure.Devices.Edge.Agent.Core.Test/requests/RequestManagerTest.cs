// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class RequestManagerTest
    {
        [Fact]
        public async Task TestProcessRequest()
        {
            // Arrange
            var requestHandler = new Mock<IRequestHandler>();
            requestHandler.Setup(r => r.HandleRequest(It.IsAny<string>()))
                .ReturnsAsync("{\"prop3\":\"foo\",\"prop4\":100}");
            var requestHandlers = new Dictionary<string, IRequestHandler>
            {
                ["req1"] = requestHandler.Object
            };
            var requestManager = new RequestManager(requestHandlers);
            string payload = "{\"prop2\":\"foo\",\"prop1\":100}";

            // Act
            (int responseStatus, string responsePayload) = await requestManager.ProcessRequest("req", payload);

            // Assert
            Assert.Equal(400, responseStatus);
            Assert.NotNull(responsePayload);
            JObject parsedJson = JObject.Parse(responsePayload);
            Assert.False(string.IsNullOrWhiteSpace(parsedJson["message"].ToString()));

            // Act
            (responseStatus, responsePayload) = await requestManager.ProcessRequest("", payload);

            // Assert
            Assert.Equal(400, responseStatus);
            Assert.NotNull(responsePayload);
            parsedJson = JObject.Parse(responsePayload);
            Assert.False(string.IsNullOrWhiteSpace(parsedJson["message"].ToString()));

            // Act
            (responseStatus, responsePayload) = await requestManager.ProcessRequest(null, payload);

            // Assert
            Assert.Equal(400, responseStatus);
            Assert.NotNull(responsePayload);
            parsedJson = JObject.Parse(responsePayload);
            Assert.False(string.IsNullOrWhiteSpace(parsedJson["message"].ToString()));

            // Act
            (responseStatus, responsePayload) = await requestManager.ProcessRequest("req1", payload);

            // Assert
            Assert.Equal(200, responseStatus);
            Assert.Equal("{\"prop3\":\"foo\",\"prop4\":100}", responsePayload);
        }

        [Theory]
        [MemberData(nameof(GetProcessRequestWithException))]
        public async Task TestProcessRequestWithHandlerException(string payload, int expectedStatus, Exception handlerException)
        {
            // Arrange
            var requestHandler = new Mock<IRequestHandler>();
            requestHandler.Setup(r => r.HandleRequest(payload)).ThrowsAsync(handlerException);
            var requestHandlers = new Dictionary<string, IRequestHandler>
            {
                ["req1"] = requestHandler.Object
            };
            var requestManager = new RequestManager(requestHandlers);

            // Act
            (int responseStatus, string responsePayload) = await requestManager.ProcessRequest("req1", payload);

            // Assert
            Assert.Equal(expectedStatus, responseStatus);
            Assert.NotNull(responsePayload);
            JObject parsedJson = JObject.Parse(responsePayload);
            Assert.False(string.IsNullOrWhiteSpace(parsedJson["message"].ToString()));
        }

        public static IEnumerable<object[]> GetProcessRequestWithException()
        {
            yield return new object[] { "{\"prop2\":\"foo\",\"prop1\":100}", 400, new ArgumentNullException() };

            yield return new object[] { "{\"prop2\":\"foo\",\"prop1\":100}", 400, new ArgumentException() };

            yield return new object[] { "{\"prop2\":\"foo\",\"prop1\":100}", 500, new InvalidOperationException() };
        }
    }
}
