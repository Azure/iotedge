// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;

    public class StoringAsyncEndpointExecutorFactory : IEndpointExecutorFactory
    {
        readonly EndpointExecutorConfig config;
        readonly AsyncEndpointExecutorOptions options;
        readonly IMessageStore messageStore;

        public StoringAsyncEndpointExecutorFactory(EndpointExecutorConfig config, AsyncEndpointExecutorOptions options, IMessageStore messageStore)
        {
            this.config = Preconditions.CheckNotNull(config, nameof(config));
            this.options = Preconditions.CheckNotNull(options, nameof(options));
            this.messageStore = Preconditions.CheckNotNull(messageStore, nameof(messageStore));
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities) => this.CreateAsync(endpoint, priorities, new NullCheckpointerFactory(), this.config);

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory) => this.CreateAsync(endpoint, priorities, checkpointerFactory, this.config);

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory, EndpointExecutorConfig endpointExecutorConfig)
        {
            Preconditions.CheckNotNull(endpoint, nameof(endpoint));
            Preconditions.CheckNotNull(checkpointerFactory, nameof(checkpointerFactory));
            Preconditions.CheckNotNull(endpointExecutorConfig, nameof(endpointExecutorConfig));

            var endpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointerFactory, endpointExecutorConfig, this.options, this.messageStore);
            await endpointExecutor.UpdatePriorities(priorities, Option.None<Endpoint>());
            return endpointExecutor;
        }
    }
}
