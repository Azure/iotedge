// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.WebSockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IWebSocketListener
    {
        string SubProtocol { get; }

        Task ProcessWebSocketRequestAsync(
            WebSocket webSocket,
            Option<EndPoint> localEndPoint,
            EndPoint remoteEndPoint,
            string correlationId);

        Task ProcessWebSocketRequestAsync(
            WebSocket webSocket,
            Option<EndPoint> localEndPoint,
            EndPoint remoteEndPoint,
            string correlationId,
            X509Certificate2 clientCert,
            IList<X509Certificate2> clientCertChain);
    }
}
