// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    class EdgeClientWebSocket : IClientWebSocket
    {
        readonly ClientWebSocket clientWebSocket;
        // TODO: Check if locking is necessary here and if separate locks can be used for reading and writing.
        readonly AsyncLock clientWebSocketLock;

        EdgeClientWebSocket(ClientWebSocket clientWebSocket)
        {
            this.clientWebSocket = Preconditions.CheckNotNull(clientWebSocket, nameof(clientWebSocket));
            this.clientWebSocketLock = new AsyncLock();
        }

        public static async Task<IClientWebSocket> Connect(Uri url, string authToken, CancellationToken cancellationToken)
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");
            await clientWebSocket.ConnectAsync(url, cancellationToken);
            return new EdgeClientWebSocket(clientWebSocket);
        }

        public WebSocketState State => this.clientWebSocket.State;

        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            using (await this.clientWebSocketLock.LockAsync(cancellationToken))
            {
                await this.clientWebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
            }
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            using (await this.clientWebSocketLock.LockAsync(cancellationToken))
            {
                return await this.clientWebSocket.ReceiveAsync(buffer, cancellationToken);
            }
        }

        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            using (await this.clientWebSocketLock.LockAsync(cancellationToken))
            {
                await this.clientWebSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
            }
        }
    }
}
