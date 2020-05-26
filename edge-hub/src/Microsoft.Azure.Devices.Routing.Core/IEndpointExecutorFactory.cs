// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    public interface IEndpointExecutorFactory
    {
        Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities);

        Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory);

        Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory, EndpointExecutorConfig endpointExecutorConfig);
    }
}
