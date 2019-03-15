// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IStreamRequestHandler
    {
        Task Handle(IClientWebSocket clientWebSocket, CancellationToken cancellationToken);
    }
}
