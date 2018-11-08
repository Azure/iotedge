// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    public interface IEndpointExecutor : IDisposable
    {
        Endpoint Endpoint { get; }

        EndpointExecutorStatus Status { get; }

        Task CloseAsync();

        Task Invoke(IMessage message);

        Task SetEndpoint(Endpoint endpoint);
    }
}
