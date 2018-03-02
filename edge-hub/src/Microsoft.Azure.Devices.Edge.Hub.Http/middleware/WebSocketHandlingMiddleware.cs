// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System;
    using System.Threading.Tasks;
    using AspNetCore.Http;
    using Microsoft.AspNetCore.Builder;
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

        public async Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                string correlationId = Guid.NewGuid().ToString();

                Events.WebSocketRequestReceived(context.TraceIdentifier, correlationId);

                await this.webSocketListenerRegistry.InvokeAsync(context, correlationId);

                Events.WebSocketRequestCompleted(context.TraceIdentifier, correlationId);
            }
            else
            {
                await this.next(context);
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<WebSocketHandlingMiddleware>();
            const int IdStart = HttpEventIds.WebSocketHandlingMiddleware;

            enum EventIds
            {
                RequestReceived = IdStart,
                RequestCompleted
            }

            public static void WebSocketRequestReceived(string traceId, string correlationId)
            {
                Log.LogDebug((int)EventIds.RequestReceived, Invariant($"Request {traceId} received. CorrelationId {correlationId}"));
            }

            public static void WebSocketRequestCompleted(string traceId, string correlationId)
            {
                Log.LogDebug((int)EventIds.RequestCompleted, Invariant($"Request {traceId} completed. CorrelationId {correlationId}"));
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
