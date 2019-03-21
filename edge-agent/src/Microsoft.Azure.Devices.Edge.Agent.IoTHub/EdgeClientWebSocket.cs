// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class EdgeClientWebSocket : IClientWebSocket
    {
        readonly ClientWebSocket clientWebSocket;

        EdgeClientWebSocket(ClientWebSocket clientWebSocket)
        {
            this.clientWebSocket = Preconditions.CheckNotNull(clientWebSocket, nameof(clientWebSocket));
        }

        public static async Task<IClientWebSocket> Connect(Uri url, string authToken, CancellationToken cancellationToken)
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");
            await clientWebSocket.ConnectAsync(url, cancellationToken);
            return new EdgeClientWebSocket(clientWebSocket);
        }

        public WebSocketState State => this.clientWebSocket.State;

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            => this.clientWebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => this.clientWebSocket.ReceiveAsync(buffer, cancellationToken);

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => this.clientWebSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
    }
}
