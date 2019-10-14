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
using System.Linq;
using System.Runtime.InteropServices.ComTypes;

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
            FactoryMetrics.AddMessage(module.Module, FactoryMetrics.Command.Start);
            return await underlying.CreateAsync(module, runtimeInfo);
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            FactoryMetrics.AddMessage(current, FactoryMetrics.Command.Start);
            FactoryMetrics.AddMessage(next.Module, FactoryMetrics.Command.Stop);
            return await underlying.UpdateAsync(current, next, runtimeInfo);
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            return await underlying.UpdateEdgeAgentAsync(module, runtimeInfo);
        }

        public async Task<ICommand> RemoveAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.Command.Stop);
            return await underlying.RemoveAsync(module);
        }
        public async Task<ICommand> StartAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.Command.Start);
            return await underlying.StartAsync(module);
        }

        public async Task<ICommand> StopAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.Command.Stop);
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

static class FactoryMetrics
{
    public enum Command
    {
        Start,
        Stop
    }

    static readonly Dictionary<Command, IMetricsCounter> counters = Enum.GetValues(typeof(Command)).Cast<Command>().ToDictionary(c => c, command =>
    {
        string commandName = Enum.GetName(typeof(Command), command).ToLower();
        return Metrics.Instance.CreateCounter(
            $"module_{commandName}",
            "Command sent to module",
            new List<string> { "module_name", "module_version" });
    });

    public static void AddMessage(Microsoft.Azure.Devices.Edge.Agent.Core.IModule module, Command command)
    {
        counters[command].Increment(1, new[] { module.Name, module.Version });
    }
}
