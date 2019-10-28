// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Edge.Util.Metrics;

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
            FactoryMetrics.AddMessage(module.Module, FactoryMetrics.ModuleCommandMetric.Start);
            using (FactoryMetrics.MeasureTime("create"))
            {
                return await this.underlying.CreateAsync(module, runtimeInfo);
            }
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            FactoryMetrics.AddMessage(current, FactoryMetrics.ModuleCommandMetric.Start);
            FactoryMetrics.AddMessage(next.Module, FactoryMetrics.ModuleCommandMetric.Stop);
            using (FactoryMetrics.MeasureTime("update"))
            {
                return await this.underlying.UpdateAsync(current, next, runtimeInfo);
            }
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            using (FactoryMetrics.MeasureTime("update"))
            {
                return await this.underlying.UpdateEdgeAgentAsync(module, runtimeInfo);
            }
        }

        public async Task<ICommand> RemoveAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.Stop);
            using (FactoryMetrics.MeasureTime("remove"))
            {
                return await this.underlying.RemoveAsync(module);
            }
        }

        public async Task<ICommand> StartAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.Start);
            using (FactoryMetrics.MeasureTime("start"))
            {
                return await this.underlying.StartAsync(module);
            }
        }

        public async Task<ICommand> StopAsync(IModule module)
        {
            FactoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.Stop);
            using (FactoryMetrics.MeasureTime("stop"))
            {
                return await this.underlying.StopAsync(module);
            }
        }

        public async Task<ICommand> RestartAsync(IModule module)
        {
            using (FactoryMetrics.MeasureTime("restart"))
            {
                return await this.underlying.RestartAsync(module);
            }
        }

        public async Task<ICommand> WrapAsync(ICommand command)
        {
            using (FactoryMetrics.MeasureTime("wrap"))
            {
                return await this.underlying.WrapAsync(command);
            }
        }
    }
}

/// <summary>
/// This exposes 1 metric of the duration of all commands, with a different tag for each command, and
/// 1 metric per deployed module, of the number of times each command is called, with a different tag for each module.
/// </summary>
static class FactoryMetrics
{
    /// <summary>
    /// Denotes commands that will have a metric per module
    /// ex: edgeagent_module_start_total{module_name="edgeHub"} 5.
    /// </summary>
    public enum ModuleCommandMetric
    {
        Start,
        Stop
    }

    static readonly Dictionary<ModuleCommandMetric, IMetricsCounter> commandCounters = Enum.GetValues(typeof(ModuleCommandMetric)).Cast<ModuleCommandMetric>().ToDictionary(c => c, command =>
    {
        string commandName = Enum.GetName(typeof(ModuleCommandMetric), command).ToLower();
        return Metrics.Instance.CreateCounter(
            $"module_{commandName}_total",
            "Command sent to module",
            new List<string> { "module_name", "module_version" });
    });

    static IMetricsDuration commandTiming = Metrics.Instance.CreateDuration(
            $"command_latency_seconds",
            "Command sent to module",
            new List<string> { "command" });

    public static void AddMessage(Microsoft.Azure.Devices.Edge.Agent.Core.IModule module, ModuleCommandMetric command)
    {
        commandCounters[command].Increment(1, new[] { module.Name, module.Version });
    }

    public static DurationSetter MeasureTime(string command)
    {
        return new DurationSetter(duration => commandTiming.Set(duration, new string[] { command }));
    }

    internal class DurationSetter : IDisposable
    {
        private Stopwatch timer = Stopwatch.StartNew();
        private Action<double> setDuration;

        internal DurationSetter(Action<double> setDuration)
        {
            this.setDuration = setDuration;
        }

        public void Dispose()
        {
            this.timer.Stop();
            this.setDuration(this.timer.Elapsed.TotalSeconds);
        }
    }
}
