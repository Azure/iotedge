// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;

    public class SyncEndpointExecutorFactory : IEndpointExecutorFactory
    {
        static readonly ICheckpointer DefaultCheckpointer = NullCheckpointer.Instance;

        readonly EndpointExecutorConfig config;

        public SyncEndpointExecutorFactory(EndpointExecutorConfig config)
        {
            this.config = Preconditions.CheckNotNull(config);
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities) => this.CreateAsync(endpoint, priorities, new NullCheckpointerFactory(), this.config);

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory) => this.CreateAsync(endpoint, priorities, checkpointerFactory, this.config);

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> _, ICheckpointerFactory checkpointerFactory, EndpointExecutorConfig endpointExecutorConfig)
        {
            ICheckpointer checkpointer = await checkpointerFactory.CreateAsync(endpoint.Id);
            IEndpointExecutor executor = new SyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig);
            return executor;
        }
    }
}
