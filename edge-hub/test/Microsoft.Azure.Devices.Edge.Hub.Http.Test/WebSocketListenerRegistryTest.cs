// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class WebSocketListenerRegistryTest
    {
        [Fact]
        public void CanRegisterAListener()
        {
            IWebSocketListener listener = this._SubprotocolListener("abc");
            var registry = new WebSocketListenerRegistry();

            Assert.True(registry.TryRegister(listener));
        }

        [Fact]
        public void CannotRegisterTheSameListenerTwice()
        {
            IWebSocketListener listener = this._SubprotocolListener("abc");
            var registry = new WebSocketListenerRegistry();

            registry.TryRegister(listener);
            Assert.False(registry.TryRegister(listener));
        }

        [Fact]
        public void CannotRegisterANullListener()
        {
            var registry = new WebSocketListenerRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.TryRegister(null));
        }

        [Fact]
        public void CannotRegisterAListenerWithoutASubProtocol()
        {
            IWebSocketListener listener = this._SubprotocolListener(null);
            var registry = new WebSocketListenerRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.TryRegister(listener));
        }

        [Fact]
        public void CanUnregisterAListener()
        {
            IWebSocketListener inListener = this._SubprotocolListener("abc");
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(inListener);

            Assert.True(registry.TryUnregister("abc", out IWebSocketListener outListener));
            Assert.Equal(inListener, outListener);
        }

        [Fact]
        public void CannotUnregisterANonExistentListener()
        {
            var registry = new WebSocketListenerRegistry();

            Assert.False(registry.TryUnregister("abc", out IWebSocketListener outListener));
            Assert.Null(outListener);
        }

        [Fact]
        public void CannotUnregisterAListenerWithANullOrWhitespaceSubProtocol()
        {
            IWebSocketListener inListener = this._SubprotocolListener("abc");
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(inListener);

            Assert.Throws<ArgumentException>(() => registry.TryUnregister(null, out IWebSocketListener _));
            Assert.Throws<ArgumentException>(() => registry.TryUnregister(string.Empty, out IWebSocketListener _));
            Assert.Throws<ArgumentException>(() => registry.TryUnregister("  ", out IWebSocketListener _));
        }

        [Fact]
        public async Task CanInvokeARegisteredListener()
        {
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(this._SubprotocolListener("abc"));
            HttpContext httpContext = this._ContextWithRequestedSubprotocols("abc");

            Assert.True(await registry.InvokeAsync(httpContext, "dontcare"));
        }

        [Fact]
        public async Task AlwaysInvokesTheFirstMatchingListener()
        {
            IWebSocketListener abcListener = this._SubprotocolListener("abc");
            IWebSocketListener xyzListener = this._SubprotocolListener("xyz");

            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(abcListener);
            registry.TryRegister(xyzListener);

            HttpContext httpContext = this._ContextWithRequestedSubprotocols("xyz", "abc");

            Assert.True(await registry.InvokeAsync(httpContext, "123"));
            Mock.Get(xyzListener).Verify(wsl => wsl.ProcessWebSocketRequestAsync(httpContext, "123"));
        }

        [Fact]
        public async Task CannotInvokeWhenNoListenersAreRegistered()
        {
            var registry = new WebSocketListenerRegistry();
            HttpContext httpContext = this._ContextWithRequestedSubprotocols("abc");

            Assert.False(await registry.InvokeAsync(httpContext, "dontcare"));
            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task CannotInvokeANonExistentListener()
        {
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(this._SubprotocolListener("abc"));
            HttpContext httpContext = this._ContextWithRequestedSubprotocols("xyz");

            Assert.False(await registry.InvokeAsync(httpContext, "dontcare"));
            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        }

        IWebSocketListener _SubprotocolListener(string subprotocol)
        {
            return Mock.Of<IWebSocketListener>(wsl => wsl.SubProtocol == subprotocol);
        }

        HttpContext _ContextWithRequestedSubprotocols(params string[] subprotocols)
        {
            return Mock.Of<HttpContext>(ctx =>
                ctx.WebSockets == Mock.Of<WebSocketManager>(wsm =>
                    wsm.WebSocketRequestedProtocols == subprotocols) &&
                ctx.Response == Mock.Of<HttpResponse>());
        }
    }
}
