// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Uds
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.Uds;
    using Xunit;

    [Unit]
    public class HttpRequestResponseSerializerTest
    {
        [Fact]
        public void TestSerializeRequest_MethodMissing_ShouldSerializeRequest()
        {
            string expected = "GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081\r\nConnection: close\r\nContent-Type: application/json\r\nContent-Length: 100\r\n\r\n";
            var request = new HttpRequestMessage();
            request.RequestUri = new Uri("http://localhost:8081/modules/testModule/sign?api-version=2018-06-28", UriKind.Absolute);
            request.Version = Version.Parse("1.1");
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/json");
            request.Content.Headers.TryAddWithoutValidation("content-length", "100");

            byte[] httpRequestData = new HttpRequestResponseSerializer().SerializeRequest(request);
            string actual = Encoding.ASCII.GetString(httpRequestData);
            Assert.Equal(expected.ToLower(), actual.ToLower());
        }

        [Fact]
        public void TestSerializeRequest_VersionMissing_ShouldSerializeRequest()
        {
            string expected = "POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081\r\nConnection: close\r\nContent-Type: application/json\r\nContent-Length: 100\r\n\r\n";
            var request = new HttpRequestMessage();
            request.RequestUri = new Uri("http://localhost:8081/modules/testModule/sign?api-version=2018-06-28", UriKind.Absolute);
            request.Method = HttpMethod.Post;
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/json");
            request.Content.Headers.TryAddWithoutValidation("content-length", "100");

            byte[] httpRequestData = new HttpRequestResponseSerializer().SerializeRequest(request);
            string actual = Encoding.ASCII.GetString(httpRequestData);
            Assert.Equal(expected.ToLower(), actual.ToLower());
        }

        [Fact]
        public void TestSerializeRequest_ContentLengthMissing_ShouldSerializeRequest()
        {
            string expected = "POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081\r\nConnection: close\r\nContent-Type: application/json\r\nContent-Length: 4\r\n\r\n";
            var request = new HttpRequestMessage();
            request.RequestUri = new Uri("http://localhost:8081/modules/testModule/sign?api-version=2018-06-28", UriKind.Absolute);
            request.Method = HttpMethod.Post;
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/json");

            byte[] httpRequestData = new HttpRequestResponseSerializer().SerializeRequest(request);
            string actual = Encoding.ASCII.GetString(httpRequestData);
            Assert.Equal(expected.ToLower(), actual.ToLower());
        }

        [Fact]
        public void TestSerializeRequest_ContentIsNull_ShouldSerializeRequest()
        {
            string expected = "GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081\r\nConnection: close\r\n\r\n";
            var request = new HttpRequestMessage();
            request.RequestUri = new Uri("http://localhost:8081/modules/testModule/sign?api-version=2018-06-28", UriKind.Absolute);
            request.Method = HttpMethod.Get;

            byte[] httpRequestData = new HttpRequestResponseSerializer().SerializeRequest(request);
            string actual = Encoding.ASCII.GetString(httpRequestData);
            Assert.Equal(expected.ToLower(), actual.ToLower());
        }

        [Fact]
        public void TestSerializeRequest_RequestIsNull_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new HttpRequestResponseSerializer().SerializeRequest(null));
        }

        [Fact]
        public void TestSerializeRequest_RequestUriIsNull_ShouldThrowArgumentNullException()
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/json");

            Assert.Throws<ArgumentNullException>(() => new HttpRequestResponseSerializer().SerializeRequest(request));
        }

        [Fact]
        public void TestSerializeRequest_ShouldSerializeRequest()
        {
            string expected = "POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nConnection: close\r\nHost: localhost:8081\r\nContent-Type: application/json\r\nContent-Length: 100";
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri("http://localhost:8081/modules/testModule/sign?api-version=2018-06-28", UriKind.Absolute);
            request.Version = Version.Parse("1.1");
            request.Headers.ConnectionClose = true;
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/json");
            request.Content.Headers.TryAddWithoutValidation("content-length", "100");

            byte[] httpRequestData = new HttpRequestResponseSerializer().SerializeRequest(request);
            string actual = Encoding.ASCII.GetString(httpRequestData);

            AssertNormalizedValues(expected, actual);
        }

        [Fact]
        public void TestDeserializeResponse_InvalidEndOfStream_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("invalid");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidStatusLine_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("invalid\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidVersion_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/11 200 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidProtocolVersionSeparator_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP-1.1 200 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidStatusCode_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 2000 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_MissingReasonPhrase_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidEndOfStatusMessage_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK \r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_StatusLine_ShouldDeserialize()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", response.ReasonPhrase);
        }

        [Fact]
        public void TestDeserializeResponse_InvalidContentLength_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 5\r\n\r\nMessage is longer");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidHeaderSeparator_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length=5\r\n\r\nMessage is longer");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidEndOfHeaders_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 5\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidHeader_ShouldDeserialize()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nTest-header: 4\r\n\r\nTest");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", response.ReasonPhrase);
            Assert.Equal(4, response.Content.Headers.ContentLength);
            Assert.Equal("Test", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task TestDeserializeResponse_ValidContent_ShouldDeserialize()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 4\r\n\r\nTest");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", response.ReasonPhrase);
            Assert.Equal(4, response.Content.Headers.ContentLength);
            Assert.Equal("Test", await response.Content.ReadAsStringAsync());
        }

        static void AssertNormalizedValues(string expected, string actual)
        {
            // Remove metacharacters before assertion to allow to run on both Windows and Linux; which Linux will return additional carriage return character.
            string normalizedExpected = Regex.Replace(expected, @"\s", string.Empty).ToLower();
            string normalizedActual = Regex.Replace(actual, @"\s", string.Empty).ToLower();
            Assert.Equal(normalizedExpected, normalizedActual);
        }
    }
}
