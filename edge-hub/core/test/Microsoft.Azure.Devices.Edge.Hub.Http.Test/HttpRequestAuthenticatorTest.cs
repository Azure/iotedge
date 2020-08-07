// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;
    using Moq;
    using Xunit;
    using CertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;

    [Unit]
    public class HttpRequestAuthenticatorTest
    {
        [Fact]
        public async Task AuthenticateRequestTest_Success()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{moduleId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.True(result.Authenticated);
            Assert.Equal(string.Empty, result.ErrorMsg);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_MultipleAuthHeaders()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(new[] { "sasToken1", "sasToken2" }));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.False(result.Authenticated);
            Assert.Equal("Invalid authorization header count", result.ErrorMsg);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_InvalidToken()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues("invalidSasToken"));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.False(result.Authenticated);
            Assert.Equal("Invalid Authorization header. Only SharedAccessSignature is supported.", result.ErrorMsg);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_TokenExpired()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{moduleId}", expired: true);
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.False(result.Authenticated);
            Assert.Equal("Cannot parse SharedAccessSignature because of the following error - The specified SAS token is expired", result.ErrorMsg);
        }

        [Fact]
        public async Task AuthenticateRequestTest_NoApiVersion_Success()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{moduleId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.True(result.Authenticated);
            Assert.Equal(string.Empty, result.ErrorMsg);
        }

        [Fact]
        public async Task InvalidCredentialsRequestTest_AuthFailed()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{moduleId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(false);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.False(result.Authenticated);
            Assert.Equal("Unable to authenticate device with Id device_2/module_1", result.ErrorMsg);
        }

        [Fact]
        public async Task AuthenticateRequestTestX509_Success()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            var clientCert = CertificateHelper.GenerateSelfSignedCert($"test_cert");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            httpContext.Connection.ClientCertificate = clientCert;
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.True(result.Authenticated);
            Assert.Equal(string.Empty, result.ErrorMsg);
        }

        [Fact]
        public async Task AuthenticateRequestTestX509IgnoresAuthorizationHeader_Success()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            var clientCert = CertificateHelper.GenerateSelfSignedCert($"test_cert");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues("blah"));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            httpContext.Connection.ClientCertificate = clientCert;
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.True(result.Authenticated);
            Assert.Equal(string.Empty, result.ErrorMsg);
        }

        [Fact]
        public async Task AuthenticateRequestX509Test_NoApiVersion_Success()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            var clientCert = CertificateHelper.GenerateSelfSignedCert($"test_cert");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues("blah"));
            httpContext.Connection.ClientCertificate = clientCert;
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.True(result.Authenticated);
            Assert.Equal(string.Empty, result.ErrorMsg);
        }

        [Fact]
        public async Task InvalidCredentialsRequestX509Test_AuthFailed()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            var clientCert = CertificateHelper.GenerateSelfSignedCert($"test_cert");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            httpContext.Connection.ClientCertificate = clientCert;
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(false);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpRequestAuthenticator(authenticator.Object, identityFactory, iothubHostName);
            HttpAuthResult result = await httpRequestAuthenticator.AuthenticateRequest(deviceId, Option.Some(moduleId), httpContext);
            Assert.False(result.Authenticated);
            Assert.Equal("Unable to authenticate device with Id device_2/module_1", result.ErrorMsg);
        }

        public class SomeException : Exception
        {
        }
    }
}
