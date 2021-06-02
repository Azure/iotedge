// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class WebSocketListenerRegistryTest
    {
        [Fact]
        public void CanRegisterAListener()
        {
            IWebSocketListener listener = this.SubprotocolListener("abc");
            var registry = new WebSocketListenerRegistry();

            Assert.True(registry.TryRegister(listener));
        }

        [Fact]
        public void CannotRegisterTheSameListenerTwice()
        {
            IWebSocketListener listener = this.SubprotocolListener("abc");
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
            IWebSocketListener listener = this.SubprotocolListener(null);
            var registry = new WebSocketListenerRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.TryRegister(listener));
        }

        [Fact]
        public void CanUnregisterAListener()
        {
            IWebSocketListener inListener = this.SubprotocolListener("abc");
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
            IWebSocketListener inListener = this.SubprotocolListener("abc");
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(inListener);

            Assert.Throws<ArgumentException>(() => registry.TryUnregister(null, out IWebSocketListener _));
            Assert.Throws<ArgumentException>(() => registry.TryUnregister(string.Empty, out IWebSocketListener _));
            Assert.Throws<ArgumentException>(() => registry.TryUnregister("  ", out IWebSocketListener _));
        }

        [Fact]
        public void CanInvokeARegisteredListener()
        {
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(this.SubprotocolListener("abc"));
            HttpContext httpContext = this.ContextWithRequestedSubprotocols("abc");

            Option<IWebSocketListener> listener = registry.GetListener(httpContext.WebSockets.WebSocketRequestedProtocols);
            Assert.True(listener.HasValue);
        }

        [Fact]
        public void AlwaysInvokesTheFirstMatchingListener()
        {
            IWebSocketListener abcListener = this.SubprotocolListener("abc");
            IWebSocketListener xyzListener = this.SubprotocolListener("xyz");

            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(abcListener);
            registry.TryRegister(xyzListener);

            HttpContext httpContext = this.ContextWithRequestedSubprotocols("xyz", "abc");

            var listener = registry.GetListener(httpContext.WebSockets.WebSocketRequestedProtocols);

            Assert.True(listener.HasValue);
            listener.ForEach(l => Assert.Equal("xyz", l.SubProtocol));
            // Mock.Get(xyzListener).Verify(wsl => wsl.ProcessWebSocketRequestAsync(It.IsAny<WebSocket>(), It.IsAny<string>(), It.IsAny<EndPoint>(), It.IsAny<EndPoint>(), It.IsAny<string>()));
        }

        [Fact]
        public void CannotInvokeWhenNoListenersAreRegistered()
        {
            var registry = new WebSocketListenerRegistry();
            HttpContext httpContext = this.ContextWithRequestedSubprotocols("abc");

            Assert.False(registry.GetListener(httpContext.WebSockets.WebSocketRequestedProtocols).HasValue);
        }

        [Fact]
        public void CannotInvokeANonExistentListener()
        {
            var registry = new WebSocketListenerRegistry();
            registry.TryRegister(this.SubprotocolListener("abc"));
            HttpContext httpContext = this.ContextWithRequestedSubprotocols("xyz");

            Assert.False(registry.GetListener(httpContext.WebSockets.WebSocketRequestedProtocols).HasValue);
        }

        IWebSocketListener SubprotocolListener(string subprotocol)
        {
            return Mock.Of<IWebSocketListener>(wsl => wsl.SubProtocol == subprotocol);
        }

        HttpContext ContextWithRequestedSubprotocols(params string[] subprotocols)
        {
            return Mock.Of<HttpContext>(
                ctx =>
                    ctx.WebSockets == Mock.Of<WebSocketManager>(
                        wsm =>
                            wsm.WebSocketRequestedProtocols == subprotocols) &&
                    ctx.Response == Mock.Of<HttpResponse>());
        }
    }
}
