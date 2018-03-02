// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
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
            var registry = Mock.Of<IWebSocketListenerRegistry>();
            HttpContext httpContext = this._WebSocketRequestContext();

            var middleware = new WebSocketHandlingMiddleware(this._ThrowingNextDelegate(), registry);
            await middleware.Invoke(httpContext);

            Mock.Get(registry).Verify(r => r.InvokeAsync(httpContext, It.IsAny<string>()));
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

        HttpContext _WebSocketRequestContext()
        {
            return Mock.Of<HttpContext>(ctx =>
                ctx.WebSockets == Mock.Of<WebSocketManager>(wsm =>
                    wsm.IsWebSocketRequest));
        }

        HttpContext _NonWebSocketRequestContext()
        {
            return Mock.Of<HttpContext>(ctx =>
                ctx.WebSockets == Mock.Of<WebSocketManager>(wsm =>
                    wsm.IsWebSocketRequest == false));
        }

        RequestDelegate _ThrowingNextDelegate()
        {
            return ctx => throw new Exception("delegate 'next' should not be called");
        }

        IWebSocketListenerRegistry _ThrowingWebSocketListenerRegistry()
        {
            var registry = new Mock<IWebSocketListenerRegistry>();

            registry
                .Setup(wslr => wslr.InvokeAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
                .Throws(new Exception("IWebSocketListenerRegistry.InvokeAsync should not be called"));

            return registry.Object;
        }

        IWebSocketListenerRegistry _ObservingWebSocketListenerRegistry(List<string> correlationIds)
        {
            var registry = new Mock<IWebSocketListenerRegistry>();

            registry
                .Setup(wslr => wslr.InvokeAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult(true))
                .Callback<HttpContext, string>((ctx, id) => correlationIds.Add(id));

            return registry.Object;
        }
    }
}
