// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    [ExcludeFromCodeCoverage]
    public class CheckpointerEndpointExecutorFactory : IEndpointExecutorFactory
    {
        readonly IEndpointExecutorFactory underlying;
        readonly ICheckpointStore store;

        public CheckpointerEndpointExecutorFactory(IEndpointExecutorFactory underlying, ICheckpointStore store)
        {
            this.underlying = underlying;
            this.store = store;
        }

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint)
        {
            ICheckpointer checkpointer = await Checkpointer.CreateAsync(endpoint.Id, this.store);
            IEndpointExecutor executor = await this.underlying.CreateAsync(endpoint, checkpointer);
            return executor;
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer)
        {
            return this.underlying.CreateAsync(endpoint, checkpointer);
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
        {
            return this.underlying.CreateAsync(endpoint, checkpointer, endpointExecutorConfig);
        }        
    }
}