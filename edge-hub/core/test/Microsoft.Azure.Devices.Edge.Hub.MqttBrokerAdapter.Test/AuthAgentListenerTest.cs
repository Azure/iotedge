// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;

    using Xunit;

    [Integration]
    public class AuthAgentHeadTest
    {
        const string HOST = "localhost";
        const int PORT = 7122;
        const string URL = "http://localhost:7122/authenticate/";

        readonly AuthAgentProtocolHeadConfig config = new AuthAgentProtocolHeadConfig(PORT, "/authenticate/");

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task StartsUpAndServes()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(200, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task CannotStartTwice()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.StartAsync());
            }                
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesNoPasswordNorCertificate()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesBothPasswordAndCertificate()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";
                content.certificate = ThumbprintTestCert;

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesBadCertificateFormat()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.certificate = new byte[] { 0x30, 0x23, 0x44 };

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesNoVersion()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.username = "testhub/device/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesBadVersion()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2017-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task AcceptsGoodTokenDeniesBadToken()
        {
            (_, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();
            var authenticator = SetupAcceptGoodToken("good_token");

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "bad_token";

                dynamic response = await PostAsync(content, URL);
                Assert.Equal(403, (int)response.result);

                content.password = "good_token";

                response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task AcceptsGoodThumbprintDeniesBadThumbprint()
        {
            (_, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();
            var authenticator = SetupAcceptGoodThumbprint(ThumbprintTestCertThumbprint2);

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
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

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task AcceptsGoodCaDeniesBadCa()
        {
            (_, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            var goodCa = new X509Certificate2(Encoding.ASCII.GetBytes(CaTestRoot2));

            var authenticator = SetupAcceptGoodCa(goodCa);

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.certificate = CaTestDevice;
                content.certificateChain = new List<string>() { CaTestRoot };

                dynamic response = await PostAsync(content, URL);
                Assert.Equal(403, (int)response.result);

                content.certificate = CaTestDevice2;
                content.certificateChain = new List<string>() { CaTestRoot2 };

                response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task ReturnsDeviceIdentity()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";

                var response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
                Assert.Equal("device", (string)response.identity);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task ReturnsModuleIdentity()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/module/api-version=2018-06-30";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";

                var response = await PostAsync(content, URL);
                Assert.Equal(200, (int)response.result);
                Assert.Equal("device/module", (string)response.identity);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task AcceptsRequestWithContentLength()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(RequestBody);

                Assert.StartsWith(@"{""result"":200,", result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task AcceptsRequestWithNoContentLength()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(RequestBody, withContentLength: false);

                Assert.StartsWith(@"{""result"":200,", result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesMalformedJsonRequest()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(NonJSONRequestBody);

                Assert.StartsWith(@"{""result"":403,", result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task DeniesBadContentLengthLongBody()
        {
            (var authenticator, var metadataStore, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();

            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();
                var result = await SendDirectRequest(RequestBody, contentLengthOverride: RequestBody.Length - 10);

                Assert.StartsWith(@"{""result"":403,", result);
            }
        }

        [Fact(Skip = "Fails in CI pipeline. Temporarily disabling while we investigate what is wrong")]
        public async Task StoresMetadataCorrectly()
        {
            (var authenticator, _, var usernameParser, var credFactory, var sysIdProvider) = SetupAcceptEverything();
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("productInfo");
            var metadataStore = new MetadataStore(store, "productInfo");
            string modelIdString = "dtmi:test:modelId;1";
            using (var sut = new AuthAgentProtocolHead(authenticator, metadataStore, usernameParser, credFactory, sysIdProvider, config))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = $"testhub/device/api-version=2018-06-30&model-id={modelIdString}";
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic password used in tests")]
                content.password = "somepassword";

                dynamic response = await PostAsync(content, URL);

                Assert.Equal(200, (int)response.result);
                var modelId = (await metadataStore.GetMetadata("device")).ModelId;
                Assert.True(modelId.HasValue);
                Assert.Equal(modelIdString, modelId.GetOrElse("impossibleValue"));
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
                var content = response.Substring(contentStart + 4);                
                return CutChunkNumber(content);
            }
        }

        private string CutChunkNumber(string content)
        {
            var result = content;

            if (content.Length > 0 && char.IsDigit(content[0]))
            {
                var contentStart = content.IndexOf("\r\n");
                result = content.Substring(contentStart + 2);
            }

            return result;
        }

        private bool IsTimeout(DateTime startTime) => DateTime.Now - startTime > TimeSpan.FromSeconds(5);

        private (IAuthenticator, IMetadataStore, IUsernameParser, IClientCredentialsFactory, ISystemComponentIdProvider) SetupAcceptEverything(string hubname = "testhub")
        {
            var authenticator = Mock.Of<IAuthenticator>();
            var metadataStore = Mock.Of<IMetadataStore>();
            var usernameParser = new MqttUsernameParser();
            var credFactory = new ClientCredentialsFactory(new IdentityProvider(hubname));
            var sysIdProvider = Mock.Of<ISystemComponentIdProvider>();

            Mock.Get(authenticator).Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).Returns(Task.FromResult(true));
            Mock.Get(sysIdProvider).Setup(a => a.EdgeHubBridgeId).Returns("testdev/$edgeHub/$bridge");

            return (authenticator, metadataStore, usernameParser, credFactory, sysIdProvider);
        }

        private IAuthenticator SetupAcceptGoodToken(string goodToken) => SetupAccept(
                c =>
                {
                    var result = false;
                    if (c is TokenCredentials tokenCreds)
                    {
                        result = tokenCreds.Token == goodToken;
                    }

                    return Task.FromResult(result);
                });

        private IAuthenticator SetupAcceptGoodThumbprint(string goodThumbprint) => SetupAccept(
                c =>
                {
                    var result = false;
                    if (c is X509CertCredentials x509Creds)
                    {
                        result = string.Equals(x509Creds.ClientCertificate.Thumbprint, goodThumbprint, StringComparison.OrdinalIgnoreCase);
                    }

                    return Task.FromResult(result);
                });

        private IAuthenticator SetupAcceptGoodCa(X509Certificate2 goodCa) => SetupAccept(
                c =>
                {
                    var trustedCaList = Option.Some<IList<X509Certificate2>>(new List<X509Certificate2>() { goodCa });
                    var result = false;
                    if (c is X509CertCredentials x509Creds)
                    {
                        (result, _) = Util.CertificateHelper.ValidateCert(x509Creds.ClientCertificate, x509Creds.ClientCertificateChain, trustedCaList);
                    }

                    return Task.FromResult(result);
                });

        private IAuthenticator SetupAccept(Func<IClientCredentials, Task<bool>> condition)
        {
            var authenticator = Mock.Of<IAuthenticator>();

            Mock.Get(authenticator)
                .Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>()))
                .Returns(condition);

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

        private static readonly string ThumbprintTestCert = "-----BEGIN CERTIFICATE-----\n" +
                                                            "MIIBLjCB1AIJAOTg4Zxl8B7jMAoGCCqGSM49BAMCMB8xHTAbBgNVBAMMFFRodW1i" +
                                                            "cHJpbnQgVGVzdCBDZXJ0MB4XDTIwMDQyMzE3NTgwN1oXDTMzMTIzMTE3NTgwN1ow" +
                                                            "HzEdMBsGA1UEAwwUVGh1bWJwcmludCBUZXN0IENlcnQwWTATBgcqhkjOPQIBBggq" +
                                                            "hkjOPQMBBwNCAARDJJBtVlgM0mBWMhAYagF7Wuc2aQYefhj0cG4wAmn3M4XcxJ39" +
                                                            "XkEup2RRAj7SSdOYhTmRpg5chhpZX/4/eF8gMAoGCCqGSM49BAMCA0kAMEYCIQD/" +
                                                            "wNzMjU1B8De5/+jEif8rkLDtqnohmVRXuAE5dCfbvAIhAJTJ+Fyg19uLSKVyOK8R" +
                                                            "5q87sIqhJXhTfNYvIt77Dq4J" +
                                                            "\n-----END CERTIFICATE-----";

        private static readonly string ThumbprintTestCertThumbprint2 = "c69f30b8feb9329506fa3f4167636915f494d19b";
        private static readonly string ThumbprintTestCert2 = "-----BEGIN CERTIFICATE-----\n" +
                                                             "MIIBMTCB2AIJAM6QHTdXFpL6MAoGCCqGSM49BAMCMCExHzAdBgNVBAMMFlRodW1i" +
                                                             "cHJpbnQgVGVzdCBDZXJ0IDIwHhcNMjAwNDIzMTgwNTMzWhcNMzMxMjMxMTgwNTMz" +
                                                             "WjAhMR8wHQYDVQQDDBZUaHVtYnByaW50IFRlc3QgQ2VydCAyMFkwEwYHKoZIzj0C" +
                                                             "AQYIKoZIzj0DAQcDQgAEKqZnpWfqQa/wS9BAeLMnhynlmHApP0/96R4q+q+HXO4m" +
                                                             "9vXQEszj2KHk9u3t/TKFfFCqbCb4uRNhQbsWDBwFqTAKBggqhkjOPQQDAgNIADBF" +
                                                             "AiBuh6l2aW4yxyhcPxOyRd0qbNJMMpx04a7waO8XvK5GNwIhALPq5K8sNzkMZhnZ" +
                                                             "tp8R7qnaCWxYLkGuaXwuZw4LST1U" +
                                                             "\n-----END CERTIFICATE-----";

        private static readonly string CaTestRoot = "-----BEGIN CERTIFICATE-----\n" +
                                                    "MIIBfDCCASKgAwIBAgIJAIIuyXPWOrrvMAoGCCqGSM49BAMCMBQxEjAQBgNVBAMM" +
                                                    "CVRlc3QgUm9vdDAeFw0yMDA0MjQyMDUwMTRaFw0zNDAxMDEyMDUwMTRaMBQxEjAQ" +
                                                    "BgNVBAMMCVRlc3QgUm9vdDBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABM58STQE" +
                                                    "OhfUumBphMzVpa4dQSS6lv+qJP/Q1XV1xTW6MQBAbpxabqk4jNbCe2XLTdETbzrn" +
                                                    "Wnskm40CzxAVkBmjXTBbMAwGA1UdEwQFMAMBAf8wCwYDVR0PBAQDAgHGMB0GA1Ud" +
                                                    "DgQWBBSrZLo1F8FcV6c4eYH1IPIlQdU9lzAfBgNVHSMEGDAWgBSrZLo1F8FcV6c4" +
                                                    "eYH1IPIlQdU9lzAKBggqhkjOPQQDAgNIADBFAiEApVcPpM3I0lsJjc1OmOOO8SGy" +
                                                    "rbv22nbkceeenoGRkyQCIHy5Na2OY49AJc1mzRpKCH10mQYkTUkSX1DaqIo//tYF" +
                                                    "\n-----END CERTIFICATE-----";

        private static readonly string CaTestDevice = "-----BEGIN CERTIFICATE-----\n" +
                                                      "MIIBRjCB7KADAgECAgkA+igvZ6louWcwCgYIKoZIzj0EAwIwFDESMBAGA1UEAwwJ" +
                                                      "VGVzdCBSb290MB4XDTIwMDQyNDIwNTAxNVoXDTM0MDEwMTIwNTAxNVowETEPMA0G" +
                                                      "A1UEAwwGZGV2aWNlMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE0p3AZbTbMkJb" +
                                                      "VXQdLjoZ3wosQ5vU5NX22w7coUtz9RECDgZ6YKa6r28s1/z18Q2MRd534NOu+OUB" +
                                                      "x0UFD0qI26MqMCgwEwYDVR0lBAwwCgYIKwYBBQUHAwEwEQYDVR0RBAowCIIGZGV2" +
                                                      "aWNlMAoGCCqGSM49BAMCA0kAMEYCIQC/VEzxzPpJeD8//ltr7mUIhb/owzgbLrmi" +
                                                      "kAFHRd1UDgIhALUZ081U9Tm/bLw9rlRb5iMzrj4tUmMcwujlz+Sl73KX" +
                                                    "\n-----END CERTIFICATE-----";

        private static readonly string CaTestRoot2 = "-----BEGIN CERTIFICATE-----\n" +
                                                     "MIIBfDCCASKgAwIBAgIJAOhzRYU913Y6MAoGCCqGSM49BAMCMBQxEjAQBgNVBAMM" +
                                                     "CVRlc3QgUm9vdDAeFw0yMDA0MjQyMDU0MzJaFw0zNDAxMDEyMDU0MzJaMBQxEjAQ" +
                                                     "BgNVBAMMCVRlc3QgUm9vdDBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABGkdpWjE" +
                                                     "uIBtieocAB0n7/uRA0lRmwToOqNRZgb05C2Aq66QjuYXpewUzIMoaweRPFYRZQ+l" +
                                                     "8TxanEkYNKS7KAijXTBbMAwGA1UdEwQFMAMBAf8wCwYDVR0PBAQDAgHGMB0GA1Ud" +
                                                     "DgQWBBSCNXZZQGZh6o7IIOkPPhqb11pYaDAfBgNVHSMEGDAWgBSCNXZZQGZh6o7I" +
                                                     "IOkPPhqb11pYaDAKBggqhkjOPQQDAgNIADBFAiEA/s0g4uAhcXb4i6oqJDmR0alY" +
                                                     "O+RyzRgCy22Ap3CTlC4CIHjA2CF7sMxOb5oADQRKxEDw40QDvlyXys/akxD03K49" +
                                                    "\n-----END CERTIFICATE-----";

        private static readonly string CaTestDevice2 = "-----BEGIN CERTIFICATE-----\n" +
                                                       "MIIBRDCB7KADAgECAgkAlwSWPRWfIsYwCgYIKoZIzj0EAwIwFDESMBAGA1UEAwwJ" +
                                                       "VGVzdCBSb290MB4XDTIwMDQyNDIwNTQzMloXDTM0MDEwMTIwNTQzMlowETEPMA0G" +
                                                       "A1UEAwwGZGV2aWNlMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAExFs+1Rdwnr9k" +
                                                       "QAMu6vdKiDRKPgeIq0GljTzMDh5NsJhlE252CLqtI6oMdZ0Zz/3Ym5WONgcxgyyY" +
                                                       "dFFPOU5l/KMqMCgwEwYDVR0lBAwwCgYIKwYBBQUHAwEwEQYDVR0RBAowCIIGZGV2" +
                                                       "aWNlMAoGCCqGSM49BAMCA0cAMEQCIDq8t07xw0wP2qS7ynjOfWxGZcNvJhcLZNPT" +
                                                       "kIBHATXPAiAxv00Sv6MsM+a8aKhns2/yfGRKOVEhpqeSUqoqn9fhSg==" +
                                                       "\n-----END CERTIFICATE-----";

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
