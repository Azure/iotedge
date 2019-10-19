// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Akka.Streams.Implementation;
using App.Metrics.Counter;
using App.Metrics.Formatters.Prometheus;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Edge.Util.Metrics;
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
            FactoryMetrics.AddMessage(module.Module, FactoryMetrics.Command.Start);
            return await this.underlying.CreateAsync(module, runtimeInfo);
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            FactoryMetrics.AddMessage(current, FactoryMetrics.Command.Start);
            FactoryMetrics.AddMessage(next.Module, FactoryMetrics.Command.Stop);
            return await this.underlying.UpdateAsync(current, next, runtimeInfo);
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            return await this.underlying.UpdateEdgeAgentAsync(module, runtimeInfo);
        }

        public async Task<ICommand> RemoveAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.Command.Stop);
            return await this.underlying.RemoveAsync(module);
        }

        public async Task<ICommand> StartAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.Command.Start);
            using (FactoryMetrics.MeasureTime(FactoryMetrics.Command.Start))
            {
                return await this.underlying.StartAsync(module);
            }
        }

        public async Task<ICommand> StopAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.Command.Stop);
            using (FactoryMetrics.MeasureTime(FactoryMetrics.Command.Stop))
            {
                return await this.underlying.StopAsync(module);
            }
        }

        public async Task<ICommand> RestartAsync(IModule module)
        {
            return await this.underlying.RestartAsync(module);
        }

        public async Task<ICommand> WrapAsync(ICommand command)
        {
            return await this.underlying.WrapAsync(command);
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

    static readonly Dictionary<Command, IMetricsCounter> commandCounters = Enum.GetValues(typeof(Command)).Cast<Command>().ToDictionary(c => c, command =>
    {
        string commandName = Enum.GetName(typeof(Command), command).ToLower();
        return Metrics.Instance.CreateCounter(
            $"module_{commandName}",
            "Command sent to module",
            new List<string> { "module_name", "module_version" });
    });

    static readonly Dictionary<Command, IMetricsDuration> commandTiming = Enum.GetValues(typeof(Command)).Cast<Command>().ToDictionary(c => c, command =>
    {
        string commandName = Enum.GetName(typeof(Command), command).ToLower();
        return Metrics.Instance.CreateDuration(
            $"{commandName}_command_latency",
            "Command sent to module",
            new List<string> { });
    });

    public static void AddMessage(Microsoft.Azure.Devices.Edge.Agent.Core.IModule module, Command command)
    {
        commandCounters[command].Increment(1, new[] { module.Name, module.Version });
    }

    public static DurationSetter MeasureTime(Command command)
    {
        return new DurationSetter(commandTiming[command]);
    }

    internal class DurationSetter : IDisposable
    {
        private Stopwatch timer = Stopwatch.StartNew();
        private IMetricsDuration metricsDuration;

        internal DurationSetter(IMetricsDuration metricsDuration)
        {
            this.metricsDuration = metricsDuration;
        }

        public void Dispose()
        {
            this.timer.Stop();
            this.metricsDuration.Set(this.timer.Elapsed.TotalSeconds, new string[] { });
        }
    }
}
