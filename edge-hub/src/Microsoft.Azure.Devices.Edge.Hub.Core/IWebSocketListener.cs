// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util;

    public interface IWebSocketListener
    {
        string SubProtocol { get; }

        Task ProcessWebSocketRequestAsync(WebSocket webSocket, Option<EndPoint> localEndPoint, EndPoint remoteEndPoint, string correlationId);
    }
}
