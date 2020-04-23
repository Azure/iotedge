// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.AuthAgent.Test
{
    using System;
    using System.Dynamic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
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
        const string URL = "http://localhost:7120/authenticate/";

        [Fact]
        public async Task StartsUpAndServes()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "somepassword";

                dynamic response = await PostStreamAsync(content, URL);

                Assert.Equal(200, (int)response.result);
            }            
        }

        [Fact]
        public async Task NoPasswordNorCertificateDenies()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";

                dynamic response = await PostStreamAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact]
        public async Task NoVersionDenies()
        {
            (var authenticator, var usernameParser, var credFactory) = SetupAcceptEverything();

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "somepassword";

                dynamic response = await PostStreamAsync(content, URL);

                Assert.Equal(403, (int)response.result);
            }
        }

        [Fact]
        public async Task AcceptsGoodTokenDeniesBadToken()
        {
            (_, var usernameParser, var credFactory) = SetupAcceptEverything();
            var authenticator = SetupAcceptGoodToken("good_token");

            using (var sut = new AuthAgentListener(authenticator, usernameParser, credFactory))
            {
                await sut.StartAsync();

                dynamic content = new ExpandoObject();
                content.version = "2020-04-20";
                content.username = "testhub/device/api-version=2018-06-30";
                content.password = "bad_token";

                dynamic response = await PostStreamAsync(content, URL);
                Assert.Equal(403, (int)response.result);

                content.password = "good_token";

                response = await PostStreamAsync(content, URL);
                Assert.Equal(200, (int)response.result);
            }
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

        private static async Task<dynamic> PostStreamAsync(dynamic content, string requestUri)
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
    }
}
