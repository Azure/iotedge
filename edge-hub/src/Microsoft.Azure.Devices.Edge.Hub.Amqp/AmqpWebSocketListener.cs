// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.WebSockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class AmqpWebSocketListener : TransportListener, IWebSocketListener
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory clientCredentialsFactory;

        public AmqpWebSocketListener(
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsFactory)
            : base(Constants.WebSocketListenerName)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
        }

        public string SubProtocol => Constants.WebSocketSubProtocol;

        public async Task ProcessWebSocketRequestAsync(WebSocket webSocket, Option<EndPoint> localEndPoint, EndPoint remoteEndPoint, string correlationId, X509Certificate2 clientCert, IList<X509Certificate2> clientCertChain)
        {
            try
            {
                var taskCompletion = new TaskCompletionSource<bool>();

                string localEndpointValue = localEndPoint.Expect(() => new ArgumentNullException(nameof(localEndPoint))).ToString();
                ServerWebSocketTransport transport;
                if ((clientCert != null) && (clientCertChain != null))
                {
                    transport = new ServerWebSocketTransport(
                        webSocket,
                        localEndpointValue,
                        remoteEndPoint.ToString(),
                        correlationId,
                        clientCert,
                        clientCertChain,
                        this.authenticator,
                        this.clientCredentialsFactory);
                }
                else
                {
                    transport = new ServerWebSocketTransport(
                        webSocket,
                        localEndpointValue,
                        remoteEndPoint.ToString(),
                        correlationId);
                }

                transport.Open();

                var args = new TransportAsyncCallbackArgs { Transport = transport, CompletedSynchronously = false };
                this.OnTransportAccepted(args);

                Events.EstablishedConnection(correlationId);

                transport.Closed += (sender, eventArgs) => { taskCompletion.SetResult(true); };

                // wait until websocket is closed
                await taskCompletion.Task;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.FailedAcceptWebSocket(correlationId, ex);
                throw;
            }
        }

        public async Task ProcessWebSocketRequestAsync(WebSocket webSocket, Option<EndPoint> localEndPoint, EndPoint remoteEndPoint, string correlationId)
            => await this.ProcessWebSocketRequestAsync(webSocket, localEndPoint, remoteEndPoint, correlationId, null, null);

        protected override void OnListen()
        {
        }

        static class Events
        {
            const int IdStart = AmqpEventIds.AmqpWebSocketListener;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AmqpWebSocketListener>();

            enum EventIds
            {
                Established = IdStart,
                Exception
            }

            public static void EstablishedConnection(string correlationId) =>
                Log.LogInformation((int)EventIds.Established, $"Connection established CorrelationId {correlationId}");

            public static void FailedAcceptWebSocket(string correlationId, Exception ex) =>
                Log.LogWarning((int)EventIds.Exception, ex, $"Connection failed CorrelationId {correlationId}");
        }
    }
}
