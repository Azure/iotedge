// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;

    public class AsyncEndpointExecutorFactory : IEndpointExecutorFactory
    {
        readonly EndpointExecutorConfig config;
        readonly AsyncEndpointExecutorOptions options;

        public AsyncEndpointExecutorFactory(EndpointExecutorConfig config, AsyncEndpointExecutorOptions options)
        {
            this.config = Preconditions.CheckNotNull(config);
            this.options = Preconditions.CheckNotNull(options);
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities) => this.CreateAsync(endpoint, priorities, new NullCheckpointerFactory(), this.config);

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory) => this.CreateAsync(endpoint, priorities, checkpointerFactory, this.config);

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> _, ICheckpointerFactory checkpointerFactory, EndpointExecutorConfig endpointExecutorConfig)
        {
            ICheckpointer checkpointer = await checkpointerFactory.CreateAsync(endpoint.Id);
            IEndpointExecutor executor = new AsyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig, this.options);
            return executor;
        }
    }
}
