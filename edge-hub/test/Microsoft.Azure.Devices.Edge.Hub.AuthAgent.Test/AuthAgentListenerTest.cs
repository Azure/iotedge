// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.AuthAgent.Test
{
    using System;
    using System.Dynamic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Moq;
    using Newtonsoft.Json;

    using Xunit;

    [Integration]
    public class AuthAgentListenerTest
    {
        const string HOST = "localhost";
        const int PORT = 7120;

        const string URL = "http://localhost:7120/authenticate/";

        [Fact]
        public async Task StartsUpAndServes()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "somepassword";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(200, (int)response.result);
            }
        }

        [Fact]
        public async Task DeniesNoPasswordNorCertificate()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact]
        public async Task DeniesBothPasswordAndCertificate()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "somepassword";
                content.certificate = ThumbprintTestCert;

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact]
        public async Task DeniesNoVersion()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "somepassword";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact]
        public async Task AcceptsGoodTokenDeniesBadToken()
        {
            (_, var usernameParser, var credFactory) = SetupAcceptEverything();
            var authenticator = SetupAcceptGoodToken("good_token");

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "bad_token";

                dynamic response = await PostAsync(content, URL);
                Assert.Equal(403, (int)response.result);

                content.password = "good_token";

                response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
            }
        }

        [Fact]
        public async Task AcceptsGoodThumbprintDeniesBadThumbprint()
        {
            (_, var usernameParser, var credFactory) = SetupAcceptEverything();
            var authenticator = SetupAcceptGoodThumbprint(ThumbprintTestCertThumbprint2);

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.certificate = ThumbprintTestCert;

                dynamic response = await PostAsync(content, URL);
                Assert.Equal(403, (int)response.result);

                content.certificate = ThumbprintTestCert2;

                response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
            }
        }

        [Fact]
        public async Task ReturnsDeviceIdentity()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "somepassword";

                var response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
                Assert.Equal("testhub/device", (string)response.identity);
            }
        }

        [Fact]
        public async Task ReturnsModuleIdentity()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/module/api-version=2018-06-30";
                content.password = "somepassword";

                var response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
                Assert.Equal("testhub/device/module", (string)response.identity);
            }
        }

        [Fact]
        public async Task AcceptsRequestWithContentLength()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(RequestBody);

                Assert.StartsWith(@"{""result"":200,", result);
            }
        }

        [Fact]
        public async Task AcceptsRequestWithNoContentLength()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(RequestBody, withContentLength: false);

                Assert.StartsWith(@"{""result"":200,", result);
            }
        }

        [Fact]
        public async Task DeniesMalformedJsonRequest()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(NonJSONRequestBody);

                Assert.StartsWith(@"{""result"":403,", result);
            }
        }

        [Fact]
        public async Task DisconnectsOnBadContentLengthShortBody()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();

                // Note, that this test pattern is different to the others. That is because HttpListener doesn't give back
                // control until it is able to read the expected content length. Because in this test we send a bigger number
                // as content length, Http Listener will keep waiting for the remaining data, then after timeout it closes the
                // connection. 
                await Assert.ThrowsAsync<IOException>(async () => await SendDirectRequest(RequestBody, contentLengthOverride: RequestBody.Length + 10));
            }
        }

        [Fact]
        public async Task DisconnectsOnBadContentLengthLongBody()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory, URL))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(RequestBody, contentLengthOverride: RequestBody.Length - 10);

                Assert.StartsWith(@"{""result"":403,", result);
            }
        }

        private async Task<string> SendDirectRequest(string content, bool withContentLength = true, int contentLengthOverride = 0)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(HOST, PORT);
                
                using (var stream = client.GetStream())
                {
                    var request = GetRequestWithBody(content, withContentLength, contentLengthOverride);
                    await stream.WriteAsync(request);
                    var response = await ReadResponse(stream);

                    return GetContentFromResponse(response);
                }
            }
        }

        private async Task<string> ReadResponse(NetworkStream stream)
        {
            var startTime = DateTime.Now;
            var readBytes = default(int);
            var readBuffer = new byte[500];

            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            do
            {
                readBytes += await stream.ReadAsync(readBuffer, readBytes, readBuffer.Length - readBytes, tokenSource.Token);
            }
            while (!IsTimeout(startTime) && readBytes == 0);

            var response = Encoding.UTF8.GetString(readBuffer, 0, readBytes);

            return response;
        }

        private byte[] GetRequestWithBody(string content, bool withContentLength, int contentLengthOverride)
        {
            var encodedBody = Encoding.UTF8.GetBytes(content);
            var contentLength = default(int);
            var chunkCloseLength = 0;
            var chunkClose = Encoding.ASCII.GetBytes("\r\n0\r\n\r\n");

            if (contentLengthOverride > 0)
            {
                contentLength = contentLengthOverride;
            }
            else
            {
                contentLength = encodedBody.Length;
            }

            var headerTemplate = default(string);
            if (withContentLength)
            {
                headerTemplate = String.Format(RequestWithContentLenTemplate, contentLength);
            }
            else
            {
                headerTemplate = String.Format(RequestWithNoContentLenTemplate, contentLength);
                chunkCloseLength = chunkClose.Length;
            }

            var header = Encoding.ASCII.GetBytes(headerTemplate);

            var request = new byte[header.Length + encodedBody.Length + chunkCloseLength];

            Array.Copy(header, request, header.Length);
            Array.Copy(encodedBody, 0, request, header.Length, encodedBody.Length);

            if (withContentLength == false)
            {
                Array.Copy(chunkClose, 0, request, request.Length - chunkCloseLength, chunkCloseLength);
            }

            return request;
        }

        private string GetContentFromResponse(string response)
        {
            var contentStart = response.IndexOf("\r\n\r\n");

            if (contentStart < 0)
            {
                return string.Empty;
            }
            else
            {
                return response.Substring(contentStart + 4);
            }
        }

        private bool IsTimeout(DateTime startTime)
        {
            return DateTime.Now - startTime > TimeSpan.FromSeconds(5);
        }

        private (IAuthenticator, IUsernameParser, IClientCredentialsFactory) SetupAcceptEverything(string hubname = "testhub")
        {
            var authenticator = Mock.Of<IAuthenticator>();
            var usernameParser = new UsernameParser();
            var credFactory = new ClientCredentialsFactory(new IdentityProvider(hubname));

            Mock.Get(authenticator).Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).Returns(Task.FromResult(true));
           
            return (authenticator, usernameParser, credFactory);
        }

        private IAuthenticator SetupAcceptGoodToken(string goodToken)
        {
            var authenticator = Mock.Of<IAuthenticator>();

            Mock.Get(authenticator)
                .Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>()))
                .Returns(
                    (IClientCredentials c) =>
                    {
                        var result = false;
                        var tokenCreds = c as TokenCredentials;
                        if (tokenCreds != null)
                        {
                            result = tokenCreds.Token == goodToken;
                        }

                        return Task.FromResult(result);
                    });

            return authenticator;
        }

        private IAuthenticator SetupAcceptGoodThumbprint(string goodThumbprint)
        {
            var authenticator = Mock.Of<IAuthenticator>();

            Mock.Get(authenticator)
                .Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>()))
                .Returns(
                    (IClientCredentials c) =>
                    {
                        var result = false;
                        var x509Creds = c as X509CertCredentials;
                        if (x509Creds != null)
                        {
                            result = string.Equals(x509Creds.ClientCertificate.Thumbprint, goodThumbprint, StringComparison.OrdinalIgnoreCase);
                        }

                        return Task.FromResult(result);
                    });

            return authenticator;
        }

        private static async Task<dynamic> PostAsync(dynamic content, string requestUri)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            using (var httpContent = CreateContent(content))
            {
                request.Content = httpContent;
                using (var response = await client.SendAsync(request))
                {
                    // Note, the AuthAgent protocol is such that it always should return 200 even in case of errors.
                    // A test never should throw here, even if it tests a failure case
                    response.EnsureSuccessStatusCode();
                    return await ReadContent(response.Content);
                }
            }
        }

        private static async Task<dynamic> ReadContent(HttpContent content)
        {
            var contentStream = await content.ReadAsStreamAsync();

            using (var streamReader = new StreamReader(contentStream, new UTF8Encoding()))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return new JsonSerializer().Deserialize<dynamic>(jsonReader);
            }
        }

        private static HttpContent CreateContent(dynamic content)
        {            
            var stream = SerializeJsonIntoStream(content);
            
            var httpContent = new StreamContent(stream);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            return httpContent;
        }

        private static MemoryStream SerializeJsonIntoStream(dynamic value)
        {
            var result = new MemoryStream();

            using (var streamWriter = new StreamWriter(result, new UTF8Encoding(false), 1024, true))
            using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.None })
            {
                new JsonSerializer().Serialize(jsonWriter, value);
            }

            result.Seek(0, SeekOrigin.Begin);

            return result;
        }

        private static readonly string ThumbprintTestCertThumbprint = "d57001602dd584cf0f7619ef11644d42dcd3505c";
        private static readonly string ThumbprintTestCert = "MIIBLjCB1AIJAOTg4Zxl8B7jMAoGCCqGSM49BAMCMB8xHTAbBgNVBAMMFFRodW1i" +
                                                            "cHJpbnQgVGVzdCBDZXJ0MB4XDTIwMDQyMzE3NTgwN1oXDTMzMTIzMTE3NTgwN1ow" +
                                                            "HzEdMBsGA1UEAwwUVGh1bWJwcmludCBUZXN0IENlcnQwWTATBgcqhkjOPQIBBggq" +
                                                            "hkjOPQMBBwNCAARDJJBtVlgM0mBWMhAYagF7Wuc2aQYefhj0cG4wAmn3M4XcxJ39" +
                                                            "XkEup2RRAj7SSdOYhTmRpg5chhpZX/4/eF8gMAoGCCqGSM49BAMCA0kAMEYCIQD/" +
                                                            "wNzMjU1B8De5/+jEif8rkLDtqnohmVRXuAE5dCfbvAIhAJTJ+Fyg19uLSKVyOK8R" +
                                                            "5q87sIqhJXhTfNYvIt77Dq4J";

        private static readonly string ThumbprintTestCertThumbprint2 = "c69f30b8feb9329506fa3f4167636915f494d19b";
        private static readonly string ThumbprintTestCert2 = "MIIBMTCB2AIJAM6QHTdXFpL6MAoGCCqGSM49BAMCMCExHzAdBgNVBAMMFlRodW1i" +
                                                             "cHJpbnQgVGVzdCBDZXJ0IDIwHhcNMjAwNDIzMTgwNTMzWhcNMzMxMjMxMTgwNTMz" +
                                                             "WjAhMR8wHQYDVQQDDBZUaHVtYnByaW50IFRlc3QgQ2VydCAyMFkwEwYHKoZIzj0C" +
                                                             "AQYIKoZIzj0DAQcDQgAEKqZnpWfqQa/wS9BAeLMnhynlmHApP0/96R4q+q+HXO4m" +
                                                             "9vXQEszj2KHk9u3t/TKFfFCqbCb4uRNhQbsWDBwFqTAKBggqhkjOPQQDAgNIADBF" +
                                                             "AiBuh6l2aW4yxyhcPxOyRd0qbNJMMpx04a7waO8XvK5GNwIhALPq5K8sNzkMZhnZ" +
                                                             "tp8R7qnaCWxYLkGuaXwuZw4LST1U";

        private static readonly string RequestWithContentLenTemplate = "POST /authenticate/ HTTP/1.1\r\n" +
                                                                       "Content-Type: application/json\r\n" +
                                                                       "Content-Length: {0}\r\n" +
                                                                       "Host: localhost\r\n\r\n";

        private static readonly string RequestWithNoContentLenTemplate = "POST /authenticate/ HTTP/1.1\r\n" +
                                                                       "Content-Type: application/json\r\n" +
                                                                       "Transfer-Encoding: chunked\r\n" +
                                                                       "Host: localhost\r\n\r\n{0:x}\r\n";

        private static readonly string RequestBody = @"{""version"":""2020-04-20"",""username"":""vikauthtest/cathumb/api-version=2018-06-30"",""password"":""somesecret""}";
        private static readonly string NonJSONRequestBody = @"[""version"":""2020-04-20"",""username"":""vikauthtest/cathumb/api-version=2018-06-30"",""password"":""somesecret""]";
    }
}
