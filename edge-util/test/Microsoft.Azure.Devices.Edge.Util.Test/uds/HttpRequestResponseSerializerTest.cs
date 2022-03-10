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
        static readonly string ChunkedResponseContentText = $"This is test content\nSecond chunk received from server\n";

        static readonly byte[] ChunkedResponseBytes =
        {
            0x48, 0x54, 0x54, 0x50, 0x2F, 0x31, 0x2E, 0x31, 0x20, 0x32, 0x30, 0x30, 0x20, 0x4F, 0x4B, 0x0D,
            0x0A, 0x74, 0x72, 0x61, 0x6E, 0x73, 0x66, 0x65, 0x72, 0x2D, 0x65, 0x6E, 0x63, 0x6F, 0x64, 0x69,
            0x6E, 0x67, 0x3A, 0x20, 0x63, 0x68, 0x75, 0x6E, 0x6B, 0x65, 0x64, 0x0D, 0x0A, 0x64, 0x61, 0x74,
            0x65, 0x3A, 0x20, 0x46, 0x72, 0x69, 0x2C, 0x20, 0x31, 0x32, 0x20, 0x41, 0x70, 0x72, 0x20, 0x32,
            0x30, 0x31, 0x39, 0x20, 0x32, 0x32, 0x3A, 0x31, 0x36, 0x3A, 0x34, 0x33, 0x20, 0x47, 0x4D, 0x54,
            0x0D, 0x0A, 0x0D, 0x0A, 0x31, 0x35, 0x0D, 0x0A, 0x54, 0x68, 0x69, 0x73, 0x20, 0x69, 0x73, 0x20,
            0x74, 0x65, 0x73, 0x74, 0x20, 0x63, 0x6f, 0x6e, 0x74, 0x65, 0x6e, 0x74, 0x0A, 0x0D, 0x0A, 0x32,
            0x32, 0x0D, 0x0A, 0x53, 0x65, 0x63, 0x6f, 0x6e, 0x64, 0x20, 0x63, 0x68, 0x75, 0x6e, 0x6b, 0x20,
            0x72, 0x65, 0x63, 0x65, 0x69, 0x76, 0x65, 0x64, 0x20, 0x66, 0x72, 0x6f, 0x6d, 0x20, 0x73, 0x65,
            0x72, 0x76, 0x65, 0x72, 0x0A, 0x0D, 0x0A, 0x30, 0x0D, 0x0A, 0x0D, 0x0A
        };

        [Fact]
        public void TestSerializeRequest_MethodMissing_ShouldSerializeRequest()
        {
            string expected = $"GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081{Environment.NewLine}Connection: close{Environment.NewLine}Content-Type: application/json{Environment.NewLine}Content-Length: 100{Environment.NewLine}\r\n";
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
            string expected = $"POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081{Environment.NewLine}Connection: close{Environment.NewLine}Content-Type: application/json{Environment.NewLine}Content-Length: 100{Environment.NewLine}\r\n";
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
            string expected = $"POST /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081{Environment.NewLine}Connection: close{Environment.NewLine}Content-Type: application/json{Environment.NewLine}Content-Length: 4{Environment.NewLine}\r\n";
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
            string expected = $"GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081{Environment.NewLine}Connection: close{Environment.NewLine}\r\n";
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
        public async Task TestDeserializeResponse_InvalidEndOfStream_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("invalid");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<IOException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidStatusLine_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("invalid\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidVersion_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/11 200 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidProtocolVersionSeparator_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP-1.1 200 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidStatusCode_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 2000 OK\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_MissingReasonPhrase_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200\r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
        }

        [Fact]
        public async Task TestDeserializeResponse_InvalidEndOfStatusMessage_ShouldThrow()
        {
            byte[] expected = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK \r\n");
            var memory = new MemoryStream(expected, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            await Assert.ThrowsAsync<IOException>(() => new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken));
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

        [Fact]
        public async Task TestDeserializeChunkedResponse_ValidContent_ShouldDeserialize()
        {
            var memory = new MemoryStream(ChunkedResponseBytes, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", response.ReasonPhrase);
            Assert.False(response.Content.Headers.ContentLength.HasValue);

            Stream responseStream = await response.Content.ReadAsStreamAsync();
            byte[] responseBytes = await ReadStream(responseStream);
            string responseText = Encoding.UTF8.GetString(responseBytes);
            Assert.Equal(ChunkedResponseContentText, responseText);
        }

        [Fact]
        public async Task TestDeserializeChunkedResponse_ValidContent_ShouldDeserialize_Sync()
        {
            var memory = new MemoryStream(ChunkedResponseBytes, true);
            var stream = new HttpBufferedStream(memory);

            CancellationToken cancellationToken = default(CancellationToken);
            HttpResponseMessage response = await new HttpRequestResponseSerializer().DeserializeResponse(stream, cancellationToken);

            Assert.Equal(response.Version, Version.Parse("1.1"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", response.ReasonPhrase);
            Assert.False(response.Content.Headers.ContentLength.HasValue);

            Stream responseStream = await response.Content.ReadAsStreamAsync();
            byte[] responseBytes = ReadStreamSync(responseStream);
            string responseText = Encoding.UTF8.GetString(responseBytes);
            Assert.Equal(ChunkedResponseContentText, responseText);
        }

        static byte[] ReadStreamSync(Stream s)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        static async Task<byte[]> ReadStream(Stream s)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = await s.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
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
