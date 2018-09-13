// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class WebSocketHandlingMiddlewareTest
    {
        [Fact]
        public void CtorThrowsWhenRequestDelegateIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WebSocketHandlingMiddleware(null, Mock.Of<IWebSocketListenerRegistry>())
                );
        }

        [Fact]
        public void CtorThrowsWhenWebSocketListenerRegistryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WebSocketHandlingMiddleware(Mock.Of<RequestDelegate>(), null)
                );
        }

        [Fact]
        public async Task InvokeAllowsExceptionsToBubbleUpToServer()
        {
            var middleware = new WebSocketHandlingMiddleware(
                (ctx) => Task.CompletedTask,
                Mock.Of<IWebSocketListenerRegistry>());

            await Assert.ThrowsAnyAsync<Exception>(() => middleware.Invoke(null));
        }

        [Fact]
        public async Task HandlesAWebSocketRequest()
        {
            HttpContext httpContext = this._ContextWithRequestedSubprotocols("abc");
            
            var listener = Mock.Of<IWebSocketListener>(wsl => wsl.SubProtocol == "abc");
            
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener);

            var middleware = new WebSocketHandlingMiddleware(this._ThrowingNextDelegate(), registry);
            await middleware.Invoke(httpContext);

            Mock.Get(listener).Verify(r => r.ProcessWebSocketRequestAsync(It.IsAny<WebSocket>(), It.IsAny<EndPoint>(), It.IsAny<EndPoint>(), It.IsAny<string>()));
        }

        [Fact]
        public async Task ProducesANewCorrelationIdForEachWebSocketRequest()
        {
            var correlationIds = new List<string>();
            IWebSocketListenerRegistry registry = this._ObservingWebSocketListenerRegistry(correlationIds);
            HttpContext httpContext = this._WebSocketRequestContext();

            var middleware = new WebSocketHandlingMiddleware(this._ThrowingNextDelegate(), registry);
            await middleware.Invoke(httpContext);
            await middleware.Invoke(httpContext);

            Assert.Equal(2, correlationIds.Count);
            Assert.NotEqual(correlationIds[0], correlationIds[1]);
        }

        [Fact]
        public async Task DoesNotHandleANonWebSocketRequest()
        {
            var next = Mock.Of<RequestDelegate>();
            HttpContext httpContext = this._NonWebSocketRequestContext();

            var middleware = new WebSocketHandlingMiddleware(next, this._ThrowingWebSocketListenerRegistry());
            await middleware.Invoke(httpContext);

            Mock.Get(next).Verify(n => n(httpContext));
        }

        [Fact]
        public async Task SetsBadrequestWhenANonExistentListener()
        {
            var listener = Mock.Of<IWebSocketListener>(wsl => wsl.SubProtocol == "abc");
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(listener);
            HttpContext httpContext = this._ContextWithRequestedSubprotocols("xyz");

            var middleware = new WebSocketHandlingMiddleware(this._ThrowingNextDelegate(), registry);
            await middleware.Invoke(httpContext);

            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task SetsBadrequestWhenNoRegisteredListener()
        {
            var registry = new WebSocketListenerRegistry();
            HttpContext httpContext = this._ContextWithRequestedSubprotocols("xyz");

            var middleware = new WebSocketHandlingMiddleware(this._ThrowingNextDelegate(), registry);
            await middleware.Invoke(httpContext);

            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        }

        HttpContext _WebSocketRequestContext()
        {
            return Mock.Of<HttpContext>(ctx =>
                ctx.WebSockets == Mock.Of<WebSocketManager>(wsm =>
                    wsm.IsWebSocketRequest) &&
                ctx.Response == Mock.Of<HttpResponse>() &&
                ctx.Connection == Mock.Of<ConnectionInfo>(conn => conn.LocalIpAddress == new IPAddress(123) && conn.LocalPort == It.IsAny<int>()
                    && conn.RemoteIpAddress == new IPAddress(123) && conn.RemotePort == It.IsAny<int>()));
        }

        HttpContext _NonWebSocketRequestContext()
        {
            return Mock.Of<HttpContext>(ctx =>
                ctx.WebSockets == Mock.Of<WebSocketManager>(wsm =>
                    wsm.IsWebSocketRequest == false));
        }

        HttpContext _ContextWithRequestedSubprotocols(params string[] subprotocols)
        {
            return Mock.Of<HttpContext>(ctx =>
                ctx.WebSockets == Mock.Of<WebSocketManager>(wsm =>
                    wsm.WebSocketRequestedProtocols == subprotocols && wsm.IsWebSocketRequest
                    && wsm.AcceptWebSocketAsync(It.IsAny<string>()) == Task.FromResult(Mock.Of<WebSocket>())) &&
                ctx.Response == Mock.Of<HttpResponse>() &&
                ctx.Connection == Mock.Of<ConnectionInfo>(conn => conn.LocalIpAddress == new IPAddress(123) && conn.LocalPort == It.IsAny<int>()
                                                           && conn.RemoteIpAddress == new IPAddress(123) && conn.RemotePort == It.IsAny<int>()));
        }

        RequestDelegate _ThrowingNextDelegate()
        {
            return ctx => throw new Exception("delegate 'next' should not be called");
        }

        IWebSocketListenerRegistry _ThrowingWebSocketListenerRegistry()
        {
            var registry = new Mock<IWebSocketListenerRegistry>();

            registry
                .Setup(wslr => wslr.GetListener(It.IsAny<IList<string>>()))
                .Throws(new Exception("IWebSocketListenerRegistry.InvokeAsync should not be called"));

            return registry.Object;
        }

        IWebSocketListenerRegistry _ObservingWebSocketListenerRegistry(List<string> correlationIds)
        {
            var registry = new Mock<IWebSocketListenerRegistry>();
            var listener = new Mock<IWebSocketListener>();

            listener.Setup(wsl => wsl.ProcessWebSocketRequestAsync(It.IsAny<WebSocket>(), It.IsAny<EndPoint>(), It.IsAny<EndPoint>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback<WebSocket, EndPoint, EndPoint, string>((ws, ep1, ep2, id) => correlationIds.Add(id));
            registry
                .Setup(wslr => wslr.GetListener(It.IsAny<IList<string>>()))
                .Returns(Option.Some(listener.Object));

            return registry.Object;
        }
    }
}
