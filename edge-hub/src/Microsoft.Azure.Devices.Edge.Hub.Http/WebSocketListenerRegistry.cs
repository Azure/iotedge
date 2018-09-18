// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class WebSocketListenerRegistry : IWebSocketListenerRegistry
    {
        readonly ConcurrentDictionary<string, IWebSocketListener> webSocketListeners;

        public WebSocketListenerRegistry()
        {
            this.webSocketListeners = new ConcurrentDictionary<string, IWebSocketListener>(StringComparer.OrdinalIgnoreCase);
        }

        public Option<IWebSocketListener> GetListener(IEnumerable<string> requestedProtocols)
        {
            foreach (string subProtocol in requestedProtocols)
            {
                if (this.webSocketListeners.TryGetValue(subProtocol, out IWebSocketListener webSocketListener))
                {
                    return Option.Some(webSocketListener);
                }
            }
            
            return Option.None<IWebSocketListener>();
        }

        public bool TryRegister(IWebSocketListener webSocketListener)
        {
            Preconditions.CheckNotNull(webSocketListener, nameof(webSocketListener));
            return this.webSocketListeners.TryAdd(webSocketListener.SubProtocol, webSocketListener);
        }

        public bool TryUnregister(string subProtocol, out IWebSocketListener webSocketListener)
        {
            Preconditions.CheckNonWhiteSpace(subProtocol, nameof(subProtocol));
            return this.webSocketListeners.TryRemove(subProtocol, out webSocketListener);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<WebSocketListenerRegistry>();
            const int IdStart = HttpEventIds.WebSocketListenerRegistry;

            enum EventIds
            {
                SubProtocolSelected = IdStart,
                SubProtocolNotSupported
            }

            public static void WebSocketSubProtocolSelected(string subProtocol, string correlationId)
            {
                Log.LogDebug((int)EventIds.SubProtocolSelected, Invariant($"SubProtocol: {subProtocol} CorrelationId: {correlationId}"));
            }

            public static void WebSocketSubProtocolNotSupported(string subProtocol, string correlationId)
            {
                Log.LogDebug((int)EventIds.SubProtocolNotSupported, Invariant($"SubProtocol: {subProtocol} CorrelationId: {correlationId}"));
            }
        }
    }
}
