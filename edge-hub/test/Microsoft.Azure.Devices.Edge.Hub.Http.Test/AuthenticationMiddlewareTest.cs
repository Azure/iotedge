// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;
    using Moq;
    using Xunit;

    [Unit]
    public class AuthenticationMiddlewareTest
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
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.True(result.success);
            Assert.Equal("", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_NoAuthHeader()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Authorization header missing", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_MultipleAuthHeaders()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(new[] { "sasToken1", "sasToken2" }));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Invalid authorization header count", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_InvalidToken()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues("invalidSasToken"));
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Invalid Authorization header. Only SharedAccessSignature is supported.", result.message);
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
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Cannot parse SharedAccessSignature because of the following error - The specified SAS token is expired", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_NoModuleId()
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

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Request header does not contain ModuleId", result.message);
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
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.True(result.success);
            Assert.Equal("", result.message);
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
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(false);

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Unable to authenticate device with Id device_2/module_1", result.message);
        }

        [Fact]
        public async Task InvalidInvokeTest_ExceptionNotCaught()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string moduleId = "module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{moduleId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.Headers.Add(HttpConstants.IdHeaderKey, $"{deviceId}/{moduleId}");
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).Throws<SomeException>();

            var identityFactory = new ClientCredentialsFactory(iothubHostName);

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory, iothubHostName);
            await Assert.ThrowsAsync<SomeException>(() => authenticationMiddleware.Invoke(httpContext));
        }

        public class SomeException : Exception { }
    }
}
