// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.WebSockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
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

            X509Certificate2 cert = await context.Connection.GetClientCertificateAsync();

            if (cert == null)
            {
                // If the connection came through the API proxy, the client cert
                // would have been forwarded in a custom header. But since TLS
                // termination occurs at the proxy, we can only trust this custom
                // header if the request came through port 8080, which an internal
                // port only accessible within the local Docker vNet.
                if (context.Connection.LocalPort == Constants.ApiProxyPort)
                {
                    if (context.Request.Headers.TryGetValue(Constants.ClientCertificateHeaderKey, out StringValues clientCertHeader) && clientCertHeader.Count > 0)
                    {
                        Events.AuthenticationApiProxy(context.Connection.RemoteIpAddress.ToString());

                        string clientCertString = WebUtility.UrlDecode(clientCertHeader.First());

                        try
                        {
                            var clientCertificateBytes = Encoding.UTF8.GetBytes(clientCertString);
                            cert = new X509Certificate2(clientCertificateBytes);
                        }
                        catch (Exception ex)
                        {
                            Events.InvalidCertificate(ex, remoteEndPoint.ToString());
                            throw;
                        }
                    }
                }
            }

            if (cert != null)
            {
                IList<X509Certificate2> certChain = context.GetClientCertificateChain();
                await listener.ProcessWebSocketRequestAsync(webSocket, localEndPoint, remoteEndPoint, correlationId, cert, certChain);
            }
            else
            {
                await listener.ProcessWebSocketRequestAsync(webSocket, localEndPoint, remoteEndPoint, correlationId);
            }

            Events.WebSocketRequestCompleted(context.TraceIdentifier, correlationId);
        }

        static class Events
        {
            const int IdStart = HttpEventIds.WebSocketHandlingMiddleware;
            static readonly ILogger Log = Logger.Factory.CreateLogger<WebSocketHandlingMiddleware>();

            enum EventIds
            {
                RequestReceived = IdStart,
                RequestCompleted,
                BadRequest,
                SubProtocolSelected,
                InvalidCertificate,
                AuthenticationApiProxy,
            }

            public static void WebSocketRequestReceived(string traceId, string correlationId) =>
                Log.LogDebug((int)EventIds.RequestReceived, Invariant($"Request {traceId} received. CorrelationId {correlationId}"));

            public static void WebSocketSubProtocolSelected(string traceId, string subProtocol, string correlationId) =>
                Log.LogDebug((int)EventIds.SubProtocolSelected, Invariant($"Request {traceId} SubProtocol: {subProtocol} CorrelationId: {correlationId}"));

            public static void WebSocketRequestCompleted(string traceId, string correlationId) =>
                Log.LogDebug((int)EventIds.RequestCompleted, Invariant($"Request {traceId} completed. CorrelationId {correlationId}"));

            public static void WebSocketRequestNoListener(string traceId, string correlationId) =>
                Log.LogDebug((int)EventIds.BadRequest, Invariant($"No listener found for request {traceId}. CorrelationId {correlationId}"));

            public static void InvalidCertificate(Exception ex, string connectionIp) =>
                Log.LogWarning((int)EventIds.InvalidCertificate, Invariant($"Invalid client certificate for incoming connection: {connectionIp}, Exception: {ex.Message}"));

            public static void AuthenticationApiProxy(string remoteAddress) =>
                Log.LogInformation((int)EventIds.AuthenticationApiProxy, $"Received authentication attempt through ApiProxy for {remoteAddress}");
        }
    }

    public static class WebSocketHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseWebSocketHandlingMiddleware(this IApplicationBuilder builder, IWebSocketListenerRegistry webSocketListenerRegistry) =>
            builder.UseMiddleware<WebSocketHandlingMiddleware>(webSocketListenerRegistry);
    }
}
