// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System.Collections.Generic;
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

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities)
        {
            ICheckpointer checkpointer = await Checkpointer.CreateAsync(endpoint.Id, this.store);
            IEndpointExecutor executor = await this.underlying.CreateAsync(endpoint, priorities, checkpointer);
            return executor;
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointer checkpointer)
        {
            return this.underlying.CreateAsync(endpoint, priorities, checkpointer);
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
        {
            return this.underlying.CreateAsync(endpoint, priorities, checkpointer, endpointExecutorConfig);
        }
    }
}
