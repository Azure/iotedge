// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using NUnit.Framework;

    /// <summary>
    /// Returns closed executors using the underlying factory
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ClosedEndpointExecutorFactory : IEndpointExecutorFactory
    {
        readonly IEndpointExecutorFactory underlying;

        public ClosedEndpointExecutorFactory(IEndpointExecutorFactory factory)
        {
            this.underlying = factory;
        }

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities)
        {
            IEndpointExecutor exec = await this.underlying.CreateAsync(endpoint, priorities);
            await exec.CloseAsync();
            return exec;
        }

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory)
        {
            IEndpointExecutor exec = await this.underlying.CreateAsync(endpoint, priorities, checkpointerFactory);
            await exec.CloseAsync();
            return exec;
        }

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, IList<uint> priorities, ICheckpointerFactory checkpointerFactory, EndpointExecutorConfig endpointExecutorConfig)
        {
            IEndpointExecutor exec = await this.underlying.CreateAsync(endpoint, priorities, checkpointerFactory, endpointExecutorConfig);
            await exec.CloseAsync();
            return exec;
        }
    }
}
