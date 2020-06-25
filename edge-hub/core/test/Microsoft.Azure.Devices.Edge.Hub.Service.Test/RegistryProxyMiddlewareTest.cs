// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Rest;
    using Moq;
    using Xunit;

    [Unit]
    public class RegistryProxyMiddlewareTest
    {
        [Theory]
        [InlineData("Get")]
        [InlineData("Post")]
        [InlineData("Put")]
        [InlineData("Delete")]
        public void TestCreateProxyRequestMessage_Verify(string requestMethod)
        {
            var contextMock = new Mock<HttpContext>();
            var requestMock = new Mock<HttpRequest>();

            contextMock.SetupGet(c => c.Request).Returns(requestMock.Object);
            requestMock.SetupGet(r => r.Method).Returns(requestMethod);
            string requestBody = "test request body";
            requestMock.SetupGet(c => c.Body).Returns(new MemoryStream(Encoding.ASCII.GetBytes(requestBody)));
            var headers = new HeaderDictionary();
            headers.Add("testkey1", "testvalue1");
            headers.Add(new KeyValuePair<string, StringValues>("testkey2", new StringValues(new string[] { "testvalue2a", "testvalue2b" })));
            requestMock.SetupGet(r => r.Headers).Returns(headers);
            var destinationUri = new Uri("http://testuri.com");

            var middleware = new RegistryProxyMiddleware(new RequestDelegate(c => { return Task.CompletedTask; }), "gateway");
            HttpRequestMessage proxyRequest = middleware.CreateProxyRequestMessage(contextMock.Object, destinationUri);

            Assert.Equal(requestMethod, proxyRequest.Method.Method);
            Assert.Equal(destinationUri, proxyRequest.RequestUri);
            Assert.Equal(destinationUri.Authority, proxyRequest.Headers.Host);
            Assert.Equal(headers.Count + 1, proxyRequest.Headers.Count());
            Assert.Equal(headers["testkey1"], proxyRequest.Headers.Where(h => h.Key == "testkey1").First().Value);
            Assert.Equal(headers["testkey2"], proxyRequest.Headers.Where(h => h.Key == "testkey2").First().Value);

            if (requestMethod.Equals("Post", StringComparison.OrdinalIgnoreCase) ||
                requestMethod.Equals("Put", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal(requestBody, proxyRequest.Content.AsString());
            }
        }

        [Fact]
        public async Task TestCopyProxyResponseAsync_VerifyNullResponse()
        {
            var contextMock = new Mock<HttpContext>();

            var middleware = new RegistryProxyMiddleware(new RequestDelegate(c => { return Task.CompletedTask; }), "gateway");
            await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.CopyProxyResponseAsync(contextMock.Object, null));
        }

        [Fact]
        public async Task TestCopyProxyResponseAsync_Verify()
        {
            var contextMock = new Mock<HttpContext>();
            var responseMock = new Mock<HttpResponse>();

            var response = responseMock.Object;
            contextMock.SetupGet(c => c.Response).Returns(response);
            var headers = new HeaderDictionary();
            var stream = new MemoryStream();
            responseMock.SetupGet(r => r.Headers).Returns(headers);
            responseMock.SetupGet(r => r.Body).Returns(stream);
            System.Net.HttpStatusCode httpStatusCode = System.Net.HttpStatusCode.OK;
            var responseMessage = new HttpResponseMessage(httpStatusCode);
            responseMessage.Headers.Add("transfer-encoding", "gzip");
            responseMessage.Headers.Add("testkey1", "testvalue1");
            responseMessage.Headers.Add("testkey2", new string[] { "testvalue2a", "testvalue2b" });
            responseMessage.Content = new StringContent("Test response text", Encoding.UTF8, "application/json");
            responseMessage.Content.Headers.Add("contentheaderkey1", "contentheadervalue1");

            var middleware = new RegistryProxyMiddleware(new RequestDelegate(c => { return Task.CompletedTask; }), "gateway");
            await middleware.CopyProxyResponseAsync(contextMock.Object, responseMessage);

            responseMock.VerifySet(r => r.StatusCode = (int)httpStatusCode);
            Assert.Equal(4, headers.Count);
            Assert.Equal("testvalue1", headers["testkey1"]);
            Assert.Equal(new string[] { "testvalue2a", "testvalue2b" }, headers["testkey2"]);
            Assert.Equal("application/json; charset=utf-8", headers["Content-Type"]);
            Assert.Equal("contentheadervalue1", headers["contentheaderkey1"]);
            Assert.Equal("Test response text", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public void TestBuildDestinationUri_VerifyWebSocketRequest()
        {
            var contextMock = new Mock<HttpContext>();
            var requestMock = new Mock<HttpRequest>();
            var webSocketManagerMock = new Mock<WebSocketManager>();

            contextMock.SetupGet(c => c.Request).Returns(requestMock.Object);
            contextMock.SetupGet(c => c.WebSockets).Returns(webSocketManagerMock.Object);
            webSocketManagerMock.SetupGet(w => w.IsWebSocketRequest).Returns(true);

            var middleware = new RegistryProxyMiddleware(new RequestDelegate(c => { return Task.CompletedTask; }), "gateway");
            Option<Uri> destinationUri = middleware.BuildDestinationUri(contextMock.Object);

            Assert.False(destinationUri.HasValue);
        }

        [Fact]
        public void TestBuildDestinationUri_VerifyIsNotHttps()
        {
            var contextMock = new Mock<HttpContext>();
            var requestMock = new Mock<HttpRequest>();
            var webSocketManagerMock = new Mock<WebSocketManager>();

            contextMock.SetupGet(c => c.Request).Returns(requestMock.Object);
            contextMock.SetupGet(c => c.WebSockets).Returns(webSocketManagerMock.Object);
            webSocketManagerMock.SetupGet(w => w.IsWebSocketRequest).Returns(false);
            requestMock.SetupGet(r => r.IsHttps).Returns(false);

            var middleware = new RegistryProxyMiddleware(new RequestDelegate(c => { return Task.CompletedTask; }), "gateway");
            Option<Uri> destinationUri = middleware.BuildDestinationUri(contextMock.Object);

            Assert.False(destinationUri.HasValue);
        }

        [Theory]
        [InlineData("/devices/d1/modules/m1", "https://gateway/devices/d1/modules/m1")]
        [InlineData("/device/d1/modules/m1", null)]
        [InlineData("/devices/d1/module/m1", null)]
        [InlineData("/devices/d1/modules", "https://gateway/devices/d1/modules")]
        [InlineData("/devices/d1/modules/", null)]
        public void TestBuildDestinationUri_VerifyRegistryPattern(string registryPath, string destUrl)
        {
            var contextMock = new Mock<HttpContext>();
            var requestMock = new Mock<HttpRequest>();
            var webSocketManagerMock = new Mock<WebSocketManager>();

            contextMock.SetupGet(c => c.Request).Returns(requestMock.Object);
            contextMock.SetupGet(c => c.WebSockets).Returns(webSocketManagerMock.Object);
            webSocketManagerMock.SetupGet(w => w.IsWebSocketRequest).Returns(false);
            requestMock.SetupGet(r => r.IsHttps).Returns(true);
            requestMock.SetupGet(r => r.Scheme).Returns("https");
            requestMock.SetupGet(r => r.Host).Returns(new HostString("testing.com"));
            requestMock.SetupGet(r => r.PathBase).Returns(string.Empty);
            requestMock.SetupGet(r => r.Path).Returns(registryPath);
            QueryString queryString = new QueryString("?api-version=2020-01-01");
            requestMock.SetupGet(r => r.QueryString).Returns(queryString);

            var middleware = new RegistryProxyMiddleware(new RequestDelegate(c => { return Task.CompletedTask; }), "gateway");
            Option<Uri> destinationUri = middleware.BuildDestinationUri(contextMock.Object);

            if (string.IsNullOrEmpty(destUrl))
            {
                Assert.False(destinationUri.HasValue);
            }
            else
            {
                Assert.Equal($"{destUrl}{queryString}", destinationUri.OrDefault().ToString());
            }
        }
    }
}
