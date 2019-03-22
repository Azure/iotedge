// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IClientWebSocket
    {
        WebSocketState State { get; }

        Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
        Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
        Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
    }
}
