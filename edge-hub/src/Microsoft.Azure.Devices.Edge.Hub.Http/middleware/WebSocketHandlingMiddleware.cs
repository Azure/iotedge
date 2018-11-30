// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using System.Security.Cryptography.X509Certificates;
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
                    async l => await this.ProcessRequestAsync(context, l, correlationId),
                    () => this.ProcessBadRequest(context, correlationId));
            }
            else
            {
                return this.next(context);
            }
        }

        Task ProcessBadRequest(HttpContext context, string correlationId)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            Events.WebSocketRequestNoListener(context.TraceIdentifier, correlationId);

            return Task.CompletedTask;
        }

        async Task ProcessRequestAsync(HttpContext context, IWebSocketListener listener, string correlationId)
        {
            Preconditions.CheckNotNull(context, nameof(context));
            Preconditions.CheckNotNull(context.Connection, nameof(context.Connection));
            Preconditions.CheckNotNull(context.Connection.RemoteIpAddress, nameof(context.Connection.RemoteIpAddress));
            Preconditions.CheckNotNull(correlationId, nameof(correlationId));

            Events.WebSocketSubProtocolSelected(context.TraceIdentifier, listener.SubProtocol, correlationId);

            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(listener.SubProtocol);
            Option<EndPoint> localEndPoint = Option.None<EndPoint>();
            if (context.Connection.LocalIpAddress != null)
            {
                localEndPoint = Option.Some<EndPoint>(new IPEndPoint(context.Connection.LocalIpAddress, context.Connection.LocalPort));
            }
            var remoteEndPoint = new IPEndPoint(context.Connection.RemoteIpAddress, context.Connection.RemotePort);
            var cert = await context.Connection.GetClientCertificateAsync();
            Option<X509Certificate2> clientCertificate = (cert == null) ? Option.None<X509Certificate2>() :
                                                                          Option.Some(cert);
            IList<X509Certificate2> certChain = null;
            if (cert != null)
            {
                certChain = context.Connection.GetClientCertificateChain(context);
            }
            Option<IList<X509Certificate2>> clientCertificateChain = (certChain == null) ? Option.None<IList<X509Certificate2>>() :
                                                                                           Option.Some(certChain);

            await listener.ProcessWebSocketRequestAsync(webSocket, localEndPoint, remoteEndPoint, correlationId, clientCertificate, clientCertificateChain);

            Events.WebSocketRequestCompleted(context.TraceIdentifier, correlationId);
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

            public static void WebSocketRequestReceived(string traceId, string correlationId) =>
                Log.LogDebug((int)EventIds.RequestReceived, Invariant($"Request {traceId} received. CorrelationId {correlationId}"));

            public static void WebSocketSubProtocolSelected(string traceId, string subProtocol, string correlationId) =>
                Log.LogDebug((int)EventIds.SubProtocolSelected, Invariant($"Request {traceId} SubProtocol: {subProtocol} CorrelationId: {correlationId}"));

            public static void WebSocketRequestCompleted(string traceId, string correlationId) =>
                Log.LogDebug((int)EventIds.RequestCompleted, Invariant($"Request {traceId} completed. CorrelationId {correlationId}"));

            public static void WebSocketRequestNoListener(string traceId, string correlationId) =>
                Log.LogDebug((int)EventIds.BadRequest, Invariant($"No listener found for request {traceId}. CorrelationId {correlationId}"));
        }
    }

    public static class WebSocketHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseWebSocketHandlingMiddleware(this IApplicationBuilder builder, IWebSocketListenerRegistry webSocketListenerRegistry) =>
            builder.UseMiddleware<WebSocketHandlingMiddleware>(webSocketListenerRegistry);
    }
}
