// Copyright (c) Microsoft. All rights reserved.
using Akka.Streams.Implementation;
using App.Metrics.Counter;
using App.Metrics.Formatters.Prometheus;
using Microsoft.Azure.Devices.Edge.Util.Metrics;
using System;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Edge.Util;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class MetricsCommandFactory : ICommandFactory
    {
        readonly ICommandFactory underlying;

        public MetricsCommandFactory(ICommandFactory underlying)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
        }

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            FactoryMetrics.AddMessage(module.Module, "create");
            return await underlying.CreateAsync(module, runtimeInfo);
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            FactoryMetrics.AddMessage(current, "update_from");
            FactoryMetrics.AddMessage(next.Module, "update_to");
            return await underlying.UpdateAsync(current, next, runtimeInfo);
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            FactoryMetrics.AddMessage(module.Module, "update");
            return await underlying.UpdateEdgeAgentAsync(module, runtimeInfo);
        }

        public async Task<ICommand> RemoveAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, "remove");
            return await underlying.RemoveAsync(module);
        }
        public async Task<ICommand> StartAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, "start");
            return await underlying.StartAsync(module);
        }

        public async Task<ICommand> StopAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, "stop");
            return await underlying.StopAsync(module);
        }

        public async Task<ICommand> RestartAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, "restart");
            return await underlying.RestartAsync(module);
        }

        public async Task<ICommand> WrapAsync(ICommand command)
        {
            return await underlying.WrapAsync(command);
        }
    }

}

static class FactoryMetrics
{
    static readonly IMetricsCounter MessagesMeter = Metrics.Instance.CreateCounter(
        "Modules",
        "Command sent to module",
        new List<string> { "ModuleName", "ModuleVersion", "Command" });

    public static void AddMessage(Microsoft.Azure.Devices.Edge.Agent.Core.IModule module, string action)
    {
        MessagesMeter.Increment(1, new[] { module.Name, module.Version, action });
    }
}
