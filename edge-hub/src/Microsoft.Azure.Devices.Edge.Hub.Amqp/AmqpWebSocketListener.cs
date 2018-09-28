// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    class AmqpWebSocketListener : TransportListener, IWebSocketListener
    {
        public string SubProtocol => Constants.WebSocketSubProtocol;

        public AmqpWebSocketListener()
            : base(Constants.WebSocketListenerName)
        {
        }

        public async Task ProcessWebSocketRequestAsync(WebSocket webSocket, Option<EndPoint> localEndPoint, EndPoint remoteEndPoint, string correlationId)
        {
            try
            {
                var taskCompletion = new TaskCompletionSource<bool>();

                string localEndpointValue = localEndPoint.Expect(() => new ArgumentNullException(nameof(localEndPoint))).ToString();
                var transport = new ServerWebSocketTransport(webSocket, localEndpointValue, remoteEndPoint.ToString(), correlationId);
                transport.Open();

                var args = new TransportAsyncCallbackArgs { Transport = transport, CompletedSynchronously = false };
                this.OnTransportAccepted(args);

                Events.EstablishedConnection(correlationId);

                transport.Closed += (sender, eventArgs) =>
                {
                    taskCompletion.SetResult(true);
                };

                //wait until websocket is closed
                await taskCompletion.Task;
            }
            catch (Exception ex) when(!ex.IsFatal())
            {
                Events.FailedAcceptWebSocket(correlationId, ex);
                throw;
            }
        }

        protected override void OnListen()
        {
            
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<AmqpWebSocketListener>();
            const int IdStart = AmqpEventIds.AmqpWebSocketListener;

            enum EventIds
            {
                Established = IdStart,
                Exception
            }

            public static void EstablishedConnection(string correlationId)
            {
                Log.LogInformation((int)EventIds.Established, $"Connection established CorrelationId {correlationId}");
            }

            public static void FailedAcceptWebSocket(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.Exception, ex, $"Connection failed CorrelationId {correlationId}");
            }
        }
    }

}
