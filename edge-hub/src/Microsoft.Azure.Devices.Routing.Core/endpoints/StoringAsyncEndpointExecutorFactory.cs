// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
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

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint) => this.CreateAsync(endpoint, NullCheckpointer.Instance, this.config);

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer) => this.CreateAsync(endpoint, checkpointer, this.config);

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
        {
            Preconditions.CheckNotNull(endpoint, nameof(endpoint));
            Preconditions.CheckNotNull(checkpointer, nameof(checkpointer));
            Preconditions.CheckNotNull(endpointExecutorConfig, nameof(endpointExecutorConfig));

            await this.messageStore.AddEndpoint(endpoint.Id);
            IEndpointExecutor endpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig, this.options, this.messageStore);
            return endpointExecutor;
        }
    }
}
