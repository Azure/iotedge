// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    public interface IEndpointExecutor : IDisposable
    {
        Endpoint Endpoint { get; }

        EndpointExecutorStatus Status { get; }

        Task Invoke(IMessage message, uint priority, uint timeToLiveSecs);

        Task SetEndpoint(Endpoint endpoint, IList<uint> priorities);

        Task CloseAsync();
    }
}
