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

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> _)
        {
            IEndpointExecutor executor = new SyncEndpointExecutor(endpoint, DefaultCheckpointer, this.config);
            return Task.FromResult(executor);
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> _, ICheckpointer checkpointer)
        {
            IEndpointExecutor executor = new SyncEndpointExecutor(endpoint, checkpointer, this.config);
            return Task.FromResult(executor);
        }

        public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> _, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
        {
            IEndpointExecutor executor = new SyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig);
            return Task.FromResult(executor);
        }
    }
}
