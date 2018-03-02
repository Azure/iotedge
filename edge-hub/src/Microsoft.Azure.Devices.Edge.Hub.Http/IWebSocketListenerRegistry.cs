// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Threading.Tasks;
    using AspNetCore.Http;

    public interface IWebSocketListenerRegistry
    {
        bool TryRegister(IWebSocketListener webSocketListener);

        bool TryUnregister(string subProtocol, out IWebSocketListener webSocketListener);

        Task<bool> InvokeAsync(HttpContext context, string correlationId);
    }
}
