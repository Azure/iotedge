// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Net;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
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
    public class HttpProxiedCertificateExtractorTest
    {
        [Fact]
        public async Task AuthenticateRequestTest_NoForwardedCertificate_ShoultReturnNone()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string apiProxyId = "iotedgeApiProxy";
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = new IPAddress(0);

            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpProxiedCertificateExtractor(authenticator.Object, identityFactory, iothubHostName, deviceId, apiProxyId);
            var cert = await httpRequestAuthenticator.GetClientCertificate(httpContext);
            Assert.Equal(Option.None<X509Certificate2>(), cert);
            authenticator.VerifyAll();
        }

        [Fact]
        public async Task AuthenticateRequestTestX509ApiProxyForward_NoSasToken_ShouldThrow()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string apiProxyId = "iotedgeApiProxy";
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = new IPAddress(0);
            var certContentBytes = CertificateHelper.GenerateSelfSignedCert($"test_cert").Export(X509ContentType.Cert);
            string certContentBase64 = Convert.ToBase64String(certContentBytes);
            string clientCertString = $"-----BEGIN CERTIFICATE-----\n{certContentBase64}\n-----END CERTIFICATE-----\n";
            clientCertString = WebUtility.UrlEncode(clientCertString);
            httpContext.Request.Headers.Add(Constants.ClientCertificateHeaderKey, new StringValues(clientCertString));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            var authenticator = new Mock<IAuthenticator>();

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpProxiedCertificateExtractor(authenticator.Object, identityFactory, iothubHostName, deviceId, apiProxyId);
            var ex = await Assert.ThrowsAsync<AuthenticationException>(() => httpRequestAuthenticator.GetClientCertificate(httpContext));
            Assert.Equal($"Unable to authorize proxy iotedgeApiProxy to forward device certificate - Authorization header missing", ex.Message);
            authenticator.VerifyAll();
        }

        [Fact]
        public async Task AuthenticateRequestTestX509ApiProxyForward_InvalidCertificate_ShoudThrow()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string apiProxyId = "iotedgeApiProxy";
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = new IPAddress(0);
            var certContentBytes = Encoding.UTF8.GetBytes("Invalid cert");
            string certContentBase64 = Convert.ToBase64String(certContentBytes);
            string clientCertString = $"{certContentBase64}";
            clientCertString = WebUtility.UrlEncode(clientCertString);
            httpContext.Request.Headers.Add(Constants.ClientCertificateHeaderKey, new StringValues(clientCertString));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{apiProxyId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            var authenticator = new Mock<IAuthenticator>();

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpProxiedCertificateExtractor(authenticator.Object, identityFactory, iothubHostName, deviceId, apiProxyId);
            await Assert.ThrowsAsync<AuthenticationException>(() => httpRequestAuthenticator.GetClientCertificate(httpContext));
            authenticator.VerifyAll();
        }

        [Fact]
        public async Task AuthenticateRequestTestX509ApiProxyForward_ProxyAuthFailed_ShouldThrow()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string apiProxyId = "iotedgeApiProxy";
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = new IPAddress(0);
            var certContentBytes = CertificateHelper.GenerateSelfSignedCert($"test_cert").Export(X509ContentType.Cert);
            string certContentBase64 = Convert.ToBase64String(certContentBytes);
            string clientCertString = $"-----BEGIN CERTIFICATE-----\n{certContentBase64}\n-----END CERTIFICATE-----\n";
            clientCertString = WebUtility.UrlEncode(clientCertString);
            httpContext.Request.Headers.Add(Constants.ClientCertificateHeaderKey, new StringValues(clientCertString));
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{apiProxyId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.Is<IClientCredentials>(c => c.Identity.Id == "device_2/iotedgeApiProxy"))).ReturnsAsync(false);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpProxiedCertificateExtractor(authenticator.Object, identityFactory, iothubHostName, deviceId, apiProxyId);
            var ex = await Assert.ThrowsAsync<AuthenticationException>(() => httpRequestAuthenticator.GetClientCertificate(httpContext));
            Assert.Equal($"Unable to authorize proxy iotedgeApiProxy to forward device certificate - Unable to authenticate proxy iotedgeApiProxy to forward certificate", ex.Message);
            authenticator.VerifyAll();
        }

        [Fact]
        public async Task AuthenticateRequestTestX509ApiProxyForward_ProxyAuthSuccess_ShouldReturnCertificate()
        {
            string iothubHostName = "TestHub.azure-devices.net";
            string deviceId = "device_2";
            string apiProxyId = "iotedgeApiProxy";
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = new IPAddress(0);
            var certContentBytes = CertificateHelper.GenerateSelfSignedCert($"test_cert").Export(X509ContentType.Cert);
            string certContentBase64 = Convert.ToBase64String(certContentBytes);
            string clientCertString = $"-----BEGIN CERTIFICATE-----\n{certContentBase64}\n-----END CERTIFICATE-----\n";
            clientCertString = WebUtility.UrlEncode(clientCertString);
            httpContext.Request.Headers.Add(Constants.ClientCertificateHeaderKey, new StringValues(clientCertString));
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{deviceId}/modules/{apiProxyId}");
            httpContext.Request.Headers.Add(HeaderNames.Authorization, new StringValues(sasToken));
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.Is<IClientCredentials>(c => c.Identity.Id == "device_2/iotedgeApiProxy"))).ReturnsAsync(true);

            var identityFactory = new ClientCredentialsFactory(new IdentityProvider(iothubHostName));

            var httpRequestAuthenticator = new HttpProxiedCertificateExtractor(authenticator.Object, identityFactory, iothubHostName, deviceId, apiProxyId);
            var cert = await httpRequestAuthenticator.GetClientCertificate(httpContext);
            Assert.True(cert.HasValue);
            authenticator.VerifyAll();
        }
    }
}
