// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System.Threading.Tasks;
    using App.Metrics;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Option = Microsoft.Azure.Devices.Edge.Util.Option;

    public class StoringAsyncEndpointExecutorFactory : IEndpointExecutorFactory
    {
        readonly EndpointExecutorConfig config;
        readonly AsyncEndpointExecutorOptions options;
        readonly IMessageStore messageStore;
        readonly Edge.Util.Option<IMetricsRoot> metricsCollector;

        public StoringAsyncEndpointExecutorFactory(EndpointExecutorConfig config, AsyncEndpointExecutorOptions options, IMessageStore messageStore, Edge.Util.Option<IMetricsRoot> metricsCollector)
        {
            this.config = Preconditions.CheckNotNull(config, nameof(config));
            this.options = Preconditions.CheckNotNull(options, nameof(options));
            this.messageStore = Preconditions.CheckNotNull(messageStore, nameof(messageStore));
            this.metricsCollector = Preconditions.CheckNotNull(metricsCollector, nameof(metricsCollector));
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint) => this.CreateAsync(endpoint, NullCheckpointer.Instance, this.config);

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer) => this.CreateAsync(endpoint, checkpointer, this.config);

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
        {
            Preconditions.CheckNotNull(endpoint, nameof(endpoint));
            Preconditions.CheckNotNull(checkpointer, nameof(checkpointer));
            Preconditions.CheckNotNull(endpointExecutorConfig, nameof(endpointExecutorConfig));

            this.messageStore.AddEndpoint(endpoint.Id);
            IEndpointExecutor endpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig, this.options, this.messageStore, this.metricsCollector);
            return Task.FromResult(endpointExecutor);
        }
    }    
}
