// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.WebSockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    [Unit]
    public class WebSocketHandlingMiddlewareTest
    {
        [Fact]
        public void CtorThrowsWhenRequestDelegateIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WebSocketHandlingMiddleware(null, Mock.Of<IWebSocketListenerRegistry>(), Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>())));
        }

        [Fact]
        public void CtorThrowsWhenWebSocketListenerRegistryIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WebSocketHandlingMiddleware(Mock.Of<RequestDelegate>(), null, Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>())));
        }

        [Fact]
        public void CtorThrowsWhenHttpProxiedCertificateExtractorIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WebSocketHandlingMiddleware(Mock.Of<RequestDelegate>(), Mock.Of<IWebSocketListenerRegistry>(), null));
        }

        [Fact]
        public async Task InvokeAllowsExceptionsToBubbleUpToServer()
        {
            var middleware = new WebSocketHandlingMiddleware(
                (ctx) => Task.CompletedTask,
                Mock.Of<IWebSocketListenerRegistry>(),
                Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>()));

            await Assert.ThrowsAnyAsync<Exception>(() => middleware.Invoke(null));
        }

        [Fact]
        public async Task HandlesAWebSocketRequest()
        {
            HttpContext httpContext = this.ContextWithRequestedSubprotocols("abc");

            var listener = Mock.Of<IWebSocketListener>(wsl => wsl.SubProtocol == "abc");

            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener);

            var middleware = new WebSocketHandlingMiddleware(this.ThrowingNextDelegate(), registry, Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>()));
            await middleware.Invoke(httpContext);

            Mock.Get(listener).Verify(r => r.ProcessWebSocketRequestAsync(It.IsAny<WebSocket>(), It.IsAny<Option<EndPoint>>(), It.IsAny<EndPoint>(), It.IsAny<string>()));
        }

        [Fact]
        public async Task ProducesANewCorrelationIdForEachWebSocketRequest()
        {
            var correlationIds = new List<string>();
            IWebSocketListenerRegistry registry = ObservingWebSocketListenerRegistry(correlationIds);
            HttpContext httpContext = this.WebSocketRequestContext();

            var middleware = new WebSocketHandlingMiddleware(this.ThrowingNextDelegate(), registry, Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>()));
            await middleware.Invoke(httpContext);
            await middleware.Invoke(httpContext);

            Assert.Equal(2, correlationIds.Count);
            Assert.NotEqual(correlationIds[0], correlationIds[1]);
        }

        [Fact]
        public async Task DoesNotHandleANonWebSocketRequest()
        {
            var next = Mock.Of<RequestDelegate>();
            HttpContext httpContext = this.NonWebSocketRequestContext();

            var middleware = new WebSocketHandlingMiddleware(next, this.ThrowingWebSocketListenerRegistry(), Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>()));
            await middleware.Invoke(httpContext);

            Mock.Get(next).Verify(n => n(httpContext));
        }

        [Fact]
        public async Task SetsBadrequestWhenANonExistentListener()
        {
            var listener = Mock.Of<IWebSocketListener>(wsl => wsl.SubProtocol == "abc");
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener);
            HttpContext httpContext = this.ContextWithRequestedSubprotocols("xyz");

            var middleware = new WebSocketHandlingMiddleware(this.ThrowingNextDelegate(), registry, Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>()));
            await middleware.Invoke(httpContext);

            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task SetsBadrequestWhenNoRegisteredListener()
        {
            var registry = new WebSocketListenerRegistry();
            HttpContext httpContext = this.ContextWithRequestedSubprotocols("xyz");

            var middleware = new WebSocketHandlingMiddleware(this.ThrowingNextDelegate(), registry, Task.FromResult(Mock.Of<IHttpProxiedCertificateExtractor>()));
            await middleware.Invoke(httpContext);

            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task UnauthorizedRequestWhenProxyAuthFails()
        {
            var next = Mock.Of<RequestDelegate>();

            var listener = new Mock<IWebSocketListener>();
            listener.Setup(wsl => wsl.SubProtocol).Returns("abc");
            listener.Setup(
                    wsl => wsl.ProcessWebSocketRequestAsync(
                        It.IsAny<WebSocket>(),
                        It.IsAny<Option<EndPoint>>(),
                        It.IsAny<EndPoint>(),
                        It.IsAny<string>(),
                        It.IsAny<X509Certificate2>(),
                        It.IsAny<IList<X509Certificate2>>(),
                        It.Is<IAuthenticator>(auth => auth != null && auth.GetType() == typeof(NullAuthenticator))))
                .Returns(Task.CompletedTask);

            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener.Object);
            var certContentBytes = Util.Test.Common.CertificateHelper.GenerateSelfSignedCert($"test_cert").Export(X509ContentType.Cert);
            string certContentBase64 = Convert.ToBase64String(certContentBytes);
            string clientCertString = $"-----BEGIN CERTIFICATE-----\n{certContentBase64}\n-----END CERTIFICATE-----\n";
            clientCertString = WebUtility.UrlEncode(clientCertString);
            HttpContext httpContext = this.ContextWithRequestedSubprotocolsAndForwardedCert(new StringValues(clientCertString), "abc");
            var certExtractor = new Mock<IHttpProxiedCertificateExtractor>();
            certExtractor.Setup(p => p.GetClientCertificate(It.IsAny<HttpContext>())).ThrowsAsync(new AuthenticationException());

            var middleware = new WebSocketHandlingMiddleware(next, registry, Task.FromResult(certExtractor.Object));
            await middleware.Invoke(httpContext);

            listener.VerifyAll();
        }

        [Fact]
        public async Task AuthorizedRequestWhenProxyAuthSuccess()
        {
            var next = Mock.Of<RequestDelegate>();

            var listener = new Mock<IWebSocketListener>();
            listener.Setup(wsl => wsl.SubProtocol).Returns("abc");
            listener.Setup(
                    wsl => wsl.ProcessWebSocketRequestAsync(
                        It.IsAny<WebSocket>(),
                        It.IsAny<Option<EndPoint>>(),
                        It.IsAny<EndPoint>(),
                        It.IsAny<string>(),
                        It.IsAny<X509Certificate2>(),
                        It.IsAny<IList<X509Certificate2>>(),
                        It.Is<IAuthenticator>(auth => auth == null)))
                .Returns(Task.CompletedTask);

            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener.Object);
            var certContentBytes = Util.Test.Common.CertificateHelper.GenerateSelfSignedCert($"test_cert").Export(X509ContentType.Cert);
            string certContentBase64 = Convert.ToBase64String(certContentBytes);
            string clientCertString = $"-----BEGIN CERTIFICATE-----\n{certContentBase64}\n-----END CERTIFICATE-----\n";
            clientCertString = WebUtility.UrlEncode(clientCertString);
            HttpContext httpContext = this.ContextWithRequestedSubprotocolsAndForwardedCert(new StringValues(clientCertString), "abc");
            var certExtractor = new Mock<IHttpProxiedCertificateExtractor>();
            certExtractor.Setup(p => p.GetClientCertificate(It.IsAny<HttpContext>())).ReturnsAsync(Option.Some(new X509Certificate2(certContentBytes)));

            var middleware = new WebSocketHandlingMiddleware(next, registry, Task.FromResult(certExtractor.Object));
            await middleware.Invoke(httpContext);

            listener.VerifyAll();
        }

        [Fact]
        public async Task AuthorizedRequestWhenCertIsNotSet()
        {
            var next = Mock.Of<RequestDelegate>();

            var listener = new Mock<IWebSocketListener>();
            listener.Setup(wsl => wsl.SubProtocol).Returns("abc");
            listener.Setup(
                    wsl => wsl.ProcessWebSocketRequestAsync(
                        It.IsAny<WebSocket>(),
                        It.IsAny<Option<EndPoint>>(),
                        It.IsAny<EndPoint>(),
                        It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener.Object);

            HttpContext httpContext = this.ContextWithRequestedSubprotocols("abc");
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(p => p.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(false);

            IHttpProxiedCertificateExtractor certExtractor = new HttpProxiedCertificateExtractor(authenticator.Object, Mock.Of<IClientCredentialsFactory>(), "hub", "edge", "proxy");

            var middleware = new WebSocketHandlingMiddleware(next, registry, Task.FromResult(certExtractor));
            await middleware.Invoke(httpContext);

            authenticator.Verify(auth => auth.AuthenticateAsync(It.IsAny<IClientCredentials>()), Times.Never);
            listener.VerifyAll();
        }

        static IWebSocketListenerRegistry ObservingWebSocketListenerRegistry(List<string> correlationIds)
        {
            var registry = new Mock<IWebSocketListenerRegistry>();
            var listener = new Mock<IWebSocketListener>();

            listener.Setup(
                    wsl => wsl.ProcessWebSocketRequestAsync(
                        It.IsAny<WebSocket>(),
                        It.IsAny<Option<EndPoint>>(),
                        It.IsAny<EndPoint>(),
                        It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback<WebSocket, Option<EndPoint>, EndPoint, string>((ws, ep1, ep2, id) => correlationIds.Add(id));
            registry
                .Setup(wslr => wslr.GetListener(It.IsAny<IList<string>>()))
                .Returns(Option.Some(listener.Object));

            return registry.Object;
        }

        HttpContext WebSocketRequestContext()
        {
            return Mock.Of<HttpContext>(
                ctx =>
                    ctx.WebSockets == Mock.Of<WebSocketManager>(wsm => wsm.IsWebSocketRequest == true)
                    && ctx.Request == Mock.Of<HttpRequest>(
                        req =>
                            req.Headers == Mock.Of<IHeaderDictionary>())
                    && ctx.Response == Mock.Of<HttpResponse>()
                    && ctx.Features == Mock.Of<IFeatureCollection>(
                        fc =>
                            fc.Get<ITlsConnectionFeatureExtended>() == Mock.Of<ITlsConnectionFeatureExtended>(
                                f => f.ChainElements == new List<X509Certificate2>()))
                    && ctx.Connection == Mock.Of<ConnectionInfo>(
                        conn => conn.LocalIpAddress == new IPAddress(123)
                                && conn.LocalPort == It.IsAny<int>()
                                && conn.RemoteIpAddress == new IPAddress(123)
                                && conn.RemotePort == It.IsAny<int>()
                                && conn.ClientCertificate == new X509Certificate2()));
        }

        HttpContext NonWebSocketRequestContext()
        {
            return Mock.Of<HttpContext>(
                ctx =>
                    ctx.WebSockets == Mock.Of<WebSocketManager>(
                        wsm =>
                            wsm.IsWebSocketRequest == false));
        }

        HttpContext ContextWithRequestedSubprotocols(params string[] subprotocols)
        {
            return Mock.Of<HttpContext>(
                ctx =>
                    ctx.WebSockets == Mock.Of<WebSocketManager>(
                        wsm =>
                            wsm.WebSocketRequestedProtocols == subprotocols
                            && wsm.IsWebSocketRequest
                            && wsm.AcceptWebSocketAsync(It.IsAny<string>()) == Task.FromResult(Mock.Of<WebSocket>()))
                    && ctx.Request == Mock.Of<HttpRequest>(
                        req =>
                            req.Headers == Mock.Of<IHeaderDictionary>())
                    && ctx.Response == Mock.Of<HttpResponse>()
                    && ctx.Features == Mock.Of<IFeatureCollection>(
                        fc => fc.Get<ITlsConnectionFeatureExtended>() == Mock.Of<ITlsConnectionFeatureExtended>(f => f.ChainElements == new List<X509Certificate2>()))
                    && ctx.Connection == Mock.Of<ConnectionInfo>(
                        conn => conn.LocalIpAddress == new IPAddress(123)
                                && conn.LocalPort == It.IsAny<int>()
                                && conn.RemoteIpAddress == new IPAddress(123) && conn.RemotePort == It.IsAny<int>()
                                && conn.ClientCertificate == new X509Certificate2()));
        }

        HttpContext ContextWithRequestedSubprotocolsAndForwardedCert(StringValues cert, params string[] subprotocols)
        {
            return Mock.Of<HttpContext>(
                ctx =>
                    ctx.WebSockets == Mock.Of<WebSocketManager>(
                        wsm =>
                            wsm.WebSocketRequestedProtocols == subprotocols
                            && wsm.IsWebSocketRequest
                            && wsm.AcceptWebSocketAsync(It.IsAny<string>()) == Task.FromResult(Mock.Of<WebSocket>()))
                    && ctx.Request == Mock.Of<HttpRequest>(
                        req =>
                            req.Headers == Mock.Of<IHeaderDictionary>(h => h.TryGetValue("x-ms-edge-clientcert", out cert)) == true )
                    && ctx.Response == Mock.Of<HttpResponse>()
                    && ctx.Features == Mock.Of<IFeatureCollection>(
                        fc => fc.Get<ITlsConnectionFeatureExtended>() == Mock.Of<ITlsConnectionFeatureExtended>(f => f.ChainElements == new List<X509Certificate2>()))
                    && ctx.Connection == Mock.Of<ConnectionInfo>(
                        conn => conn.LocalIpAddress == new IPAddress(123)
                                && conn.LocalPort == It.IsAny<int>()
                                && conn.RemoteIpAddress == new IPAddress(123) && conn.RemotePort == It.IsAny<int>()
                                && conn.ClientCertificate == new X509Certificate2()));
        }

        RequestDelegate ThrowingNextDelegate()
        {
            return ctx => throw new Exception("delegate 'next' should not be called");
        }

        IWebSocketListenerRegistry ThrowingWebSocketListenerRegistry()
        {
            var registry = new Mock<IWebSocketListenerRegistry>();

            registry
                .Setup(wslr => wslr.GetListener(It.IsAny<IList<string>>()))
                .Throws(new Exception("IWebSocketListenerRegistry.InvokeAsync should not be called"));

            return registry.Object;
        }
    }
}
