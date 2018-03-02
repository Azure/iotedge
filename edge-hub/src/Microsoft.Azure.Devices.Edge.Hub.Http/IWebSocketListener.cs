// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    public interface IWebSocketListener
    {
        string SubProtocol { get; }

        Task ProcessWebSocketRequestAsync(HttpContext listenerContext, string correlationId);
    }
}
