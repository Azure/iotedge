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
        readonly FactoryMetrics factoryMetrics;

        public MetricsCommandFactory(ICommandFactory underlying, IMetricsProvider metricsProvider)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.factoryMetrics = new FactoryMetrics(metricsProvider);
        }

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            this.factoryMetrics.AddMessage(module.Module, FactoryMetrics.ModuleCommandMetric.Start);
            using (this.factoryMetrics.MeasureTime("create"))
            {
                return await this.underlying.CreateAsync(module, runtimeInfo);
            }
        }

        public async Task<ICommand> PrepareUpdateAsync(IModule module, IRuntimeInfo runtimeInfo)
        {
            this.factoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.PrepareUpdate);
            using (this.factoryMetrics.MeasureTime("prepareUpdate"))
            {
                return await this.underlying.PrepareUpdateAsync(module, runtimeInfo);
            }
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            this.factoryMetrics.AddMessage(current, FactoryMetrics.ModuleCommandMetric.Start);
            this.factoryMetrics.AddMessage(next.Module, FactoryMetrics.ModuleCommandMetric.Stop);
            using (this.factoryMetrics.MeasureTime("update"))
            {
                return await this.underlying.UpdateAsync(current, next, runtimeInfo);
            }
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            using (this.factoryMetrics.MeasureTime("update"))
            {
                return await this.underlying.UpdateEdgeAgentAsync(module, runtimeInfo);
            }
        }

        public async Task<ICommand> RemoveAsync(IModule module)
        {
            this.factoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.Stop);
            using (this.factoryMetrics.MeasureTime("remove"))
            {
                return await this.underlying.RemoveAsync(module);
            }
        }

        public async Task<ICommand> StartAsync(IModule module)
        {
            this.factoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.Start);
            using (this.factoryMetrics.MeasureTime("start"))
            {
                return await this.underlying.StartAsync(module);
            }
        }

        public async Task<ICommand> StopAsync(IModule module)
        {
            this.factoryMetrics.AddMessage(module, FactoryMetrics.ModuleCommandMetric.Stop);
            using (this.factoryMetrics.MeasureTime("stop"))
            {
                return await this.underlying.StopAsync(module);
            }
        }

        public async Task<ICommand> RestartAsync(IModule module)
        {
            using (this.factoryMetrics.MeasureTime("restart"))
            {
                return await this.underlying.RestartAsync(module);
            }
        }

        public async Task<ICommand> WrapAsync(ICommand command)
        {
            using (this.factoryMetrics.MeasureTime("wrap"))
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
public class FactoryMetrics
{
    /// <summary>
    /// Denotes commands that will have a metric per module
    /// ex: edgeagent_module_start_total{module_name="edgeHub"} 5.
    /// </summary>
    public enum ModuleCommandMetric
    {
        Start,
        Stop,
        PrepareUpdate
    }

    readonly Dictionary<ModuleCommandMetric, IMetricsCounter> commandCounters;
    readonly IMetricsDuration commandTiming;

    public FactoryMetrics(IMetricsProvider metricsProvider)
    {
        this.commandCounters = Enum.GetValues(typeof(ModuleCommandMetric)).Cast<ModuleCommandMetric>().ToDictionary(c => c, command =>
        {
            string commandName = Enum.GetName(typeof(ModuleCommandMetric), command).ToLower();
            if (commandName == ModuleCommandMetric.PrepareUpdate.ToString().ToLower())
            {
                commandName = "prepare_update";
            }

            return metricsProvider.CreateCounter(
                $"module_{commandName}",
                "Command sent to module",
                new List<string> { "module_name", "module_version", MetricsConstants.MsTelemetry });
        });

        this.commandTiming = metricsProvider.CreateDuration(
            $"command_latency",
            "Command sent to module",
            new List<string> { "command", MetricsConstants.MsTelemetry });
    }

    public void AddMessage(Microsoft.Azure.Devices.Edge.Agent.Core.IModule module, ModuleCommandMetric command)
    {
        // TODO: determine if module name is PII
        this.commandCounters[command].Increment(1, new[] { module.Name, module.Version, true.ToString() });
    }

    public IDisposable MeasureTime(string command)
    {
        return DurationMeasurer.MeasureDuration(duration => this.commandTiming.Set(duration.TotalSeconds, new string[] { command, true.ToString() }));
    }
}
