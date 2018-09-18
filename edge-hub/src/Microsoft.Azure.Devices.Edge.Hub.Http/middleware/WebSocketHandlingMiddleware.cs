// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using AspNetCore.Http;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class WebSocketHandlingMiddleware
    {
        readonly RequestDelegate next;
        readonly IWebSocketListenerRegistry webSocketListenerRegistry;

        public WebSocketHandlingMiddleware(RequestDelegate next, IWebSocketListenerRegistry webSocketListenerRegistry)
        {
            this.next = Preconditions.CheckNotNull(next, nameof(next));
            this.webSocketListenerRegistry = Preconditions.CheckNotNull(webSocketListenerRegistry, nameof(webSocketListenerRegistry));
        }

        public Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                string correlationId = Guid.NewGuid().ToString();

                Events.WebSocketRequestReceived(context.TraceIdentifier, correlationId);

                Option<IWebSocketListener> listener = this.webSocketListenerRegistry.GetListener(context.WebSockets.WebSocketRequestedProtocols);
                return listener.Match(
                    async l =>
                    {
                        Events.WebSocketSubProtocolSelected(context.TraceIdentifier, l.SubProtocol, correlationId);

                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(l.SubProtocol);
                        var localEndPoint = new IPEndPoint(context.Connection.LocalIpAddress, context.Connection.LocalPort);
                        var remoteEndPoint = new IPEndPoint(context.Connection.RemoteIpAddress, context.Connection.RemotePort);
                        await l.ProcessWebSocketRequestAsync(webSocket, localEndPoint, remoteEndPoint, correlationId);

                        Events.WebSocketRequestCompleted(context.TraceIdentifier, correlationId);
                    },
                    () =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        Events.WebSocketRequestNoListener(context.TraceIdentifier, correlationId);

                        return Task.CompletedTask;
                    });
            }
            else
            {
                return this.next(context);
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<WebSocketHandlingMiddleware>();
            const int IdStart = HttpEventIds.WebSocketHandlingMiddleware;

            enum EventIds
            {
                RequestReceived = IdStart,
                RequestCompleted,
                BadRequest,
                SubProtocolSelected
            }

            public static void WebSocketRequestReceived(string traceId, string correlationId)
            {
                Log.LogDebug((int)EventIds.RequestReceived, Invariant($"Request {traceId} received. CorrelationId {correlationId}"));
            }

            public static void WebSocketSubProtocolSelected(string traceId, string subProtocol, string correlationId)
            {
                Log.LogDebug((int)EventIds.SubProtocolSelected, Invariant($"Request {traceId} SubProtocol: {subProtocol} CorrelationId: {correlationId}"));
            }

            public static void WebSocketRequestCompleted(string traceId, string correlationId)
            {
                Log.LogDebug((int)EventIds.RequestCompleted, Invariant($"Request {traceId} completed. CorrelationId {correlationId}"));
            }

            public static void WebSocketRequestNoListener(string traceId, string correlationId)
            {
                Log.LogDebug((int)EventIds.BadRequest, Invariant($"No listener found for request {traceId}. CorrelationId {correlationId}"));
            }
        }
    }

    public static class WebSocketHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseWebSocketHandlingMiddleware(this IApplicationBuilder builder, IWebSocketListenerRegistry webSocketListenerRegistry)
        {
            return builder.UseMiddleware<WebSocketHandlingMiddleware>(webSocketListenerRegistry);
        }
    }
}
