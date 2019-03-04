// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

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

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint)
        {
            IEndpointExecutor exec = await this.underlying.CreateAsync(endpoint);
            await exec.CloseAsync();
            return exec;
        }

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer)
        {
            IEndpointExecutor exec = await this.underlying.CreateAsync(endpoint, checkpointer);
            await exec.CloseAsync();
            return exec;
        }

        public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
        {
            IEndpointExecutor exec = await this.underlying.CreateAsync(endpoint, checkpointer, endpointExecutorConfig);
            await exec.CloseAsync();
            return exec;
        }
    }
}
