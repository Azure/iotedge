// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IStreamRequestHandler
    {
        Task Handle(ClientWebSocket clientWebSocket, CancellationToken cancellationToken);
    }
}
