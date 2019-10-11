// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MetricsCommandFactory : ICommandFactory
    {
        readonly ICommandFactory underlying;

        public MetricsCommandFactory(ICommandFactory underlying)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
        }

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            return await underlying.CreateAsync(module, runtimeInfo);
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            return await underlying.UpdateAsync(current, next, runtimeInfo);
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            return await underlying.UpdateEdgeAgentAsync(module, runtimeInfo);
        }

        public async Task<ICommand> RemoveAsync(IModule module)
        {
            return await underlying.RemoveAsync(module);
        }
        public async Task<ICommand> StartAsync(IModule module)
        {
            return await underlying.StartAsync(module);
        }

        public async Task<ICommand> StopAsync(IModule module)
        {
            return await underlying.StopAsync(module);
        }

        public async Task<ICommand> RestartAsync(IModule module)
        {
            return await underlying.RestartAsync(module);
        }

        public async Task<ICommand> WrapAsync(ICommand command)
        {
            return await underlying.WrapAsync(command);
        }


    }
}
