// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Uds
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Uds;
    using Xunit;

    public class HttpRequestResponseSerializerTest
    {
        [Fact]
        public void TestSerializeRequest_MethodMissing_ShouldSerializeRequest()
        {
            string expected = @"GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1
Host: localhost:8081
Connection: close
Content-Type: application/json
Content-Length: 100

";
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
            string expected = @"POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1
Host: localhost:8081
Connection: close
Content-Type: application/json
Content-Length: 100

";
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
            string expected = @"POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1
Host: localhost:8081
Connection: close
Content-Type: application/json
Content-Length: 4

";
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
            string expected = @"GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1
Host: localhost:8081
Connection: close

";
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
            string expected = @"POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1
Connection: close
Host: localhost:8081
Content-Type: application/json
Content-Length: 100

";
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
            Assert.Equal(expected.ToLower(), actual.ToLower());
        }

        [Fact]
        public void TestDeserializeResponse_InvalidEndOfStream_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("invalid");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidStatusLine_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("invalid\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidVersion_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/11 200 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidProtocolVersionSeparator_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP-1.1 200 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidStatusCode_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 2000 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_MissingReasonPhrase_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidEndOfStatusMessage_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK \r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_StatusLine_ShouldDeserialize()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(response.StatusCode, System.Net.HttpStatusCode.OK);
            Assert.Equal(response.ReasonPhrase, "OK");
        }

        [Fact]
        public void TestDeserializeResponse_InvalidContentLength_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 5\r\n\r\nMessage is longer");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidHeaderSeparator_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length=5\r\n\r\nMessage is longer");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public void TestDeserializeResponse_InvalidEndOfHeaders_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 5\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidHeader_ShouldDeserialize()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nTest-header: 4\r\n\r\nTest");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(response.StatusCode, System.Net.HttpStatusCode.OK);
            Assert.Equal(response.ReasonPhrase, "OK");
            Assert.Equal(response.Content.Headers.ContentLength, 4);
            Assert.Equal(await response.Content.ReadAsStringAsync(), "Test");
        }

        [Fact]
        public async Task TestDeserializeResponse_ValidContent_ShouldDeserialize()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 4\r\n\r\nTest");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(response.StatusCode, System.Net.HttpStatusCode.OK);
            Assert.Equal(response.ReasonPhrase, "OK");
            Assert.Equal(response.Content.Headers.ContentLength, 4);
            Assert.Equal(await response.Content.ReadAsStringAsync(), "Test");
        }
    }
}
