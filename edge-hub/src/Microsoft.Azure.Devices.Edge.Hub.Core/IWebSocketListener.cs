// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;

    public interface IWebSocketListener
    {
        string SubProtocol { get; }

        Task ProcessWebSocketRequestAsync(WebSocket webSocket, EndPoint localEndPoint, EndPoint remoteEndPoint, string correlationId);
    }
}
