// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;
    using Moq;
    using Xunit;
    using Microsoft.Extensions.Caching.Memory;

    [Unit]
    public class AuthenticationMiddlewareTest
    {
        [Fact]
        public async Task AuthenticateRequestTest_Success()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2/modules/module_1");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.Headers.Add(HttpConstants.ModuleIdHeaderKey, id);

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(true);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.True(result.success);
            Assert.Equal("", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_NoAuthHeader()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(true);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Authorization header missing", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_MultipleAuthHeaders()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(new string[] { "sasToken1", "sasToken2" }));

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(true);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Invalid authorization header count", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_InvalidToken()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(new string[] { "sasToken1" }));

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(true);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Invalid Authorization header. Only SharedAccessSignature is supported.", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_TokenExpired()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2/modules/module_1", expired: true);
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(true);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Cannot parse SharedAccessSignature because of the following error - The specified SAS token is expired", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_NoDeviceId()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2/modules/module_1");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(true);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Request header does not contain ModuleId", result.message);
        }

        [Fact]
        public async Task InvalidAuthenticateRequestTest_AuthFailed()
        {
            string id = "device_2/module_1";
            var httpContext = new DefaultHttpContext();
            string sasToken = TokenHelper.CreateSasToken("TestHub.azure-devices.net/devices/device_2/modules/module_1");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.Headers.Add(HttpConstants.ModuleIdHeaderKey, id);

            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IIdentity>())).ReturnsAsync(false);

            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var identityFactory = new Mock<IIdentityFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.Is<string>(d => d == "TestHub.azure-devices.net/" + id), It.IsAny<string>())).Returns(Try.Success(identity));

            var authenticationMiddleware = new AuthenticationMiddleware(Mock.Of<RequestDelegate>(), authenticator.Object, identityFactory.Object, "TestHub.azure-devices.net", new MemoryCache(new MemoryCacheOptions()));
            (bool success, string message) result = await authenticationMiddleware.AuthenticateRequest(httpContext);
            Assert.NotNull(result);
            Assert.False(result.success);
            Assert.Equal("Unable to authenticate device with Id device_2/module_1", result.message);
        }
    }
}
