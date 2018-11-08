// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    public interface IEndpointExecutorFactory
    {
        Task<IEndpointExecutor> CreateAsync(Endpoint endpoint);

        Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer);

        Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig);
    }
}
