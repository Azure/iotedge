// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This is a simple deployment strategy. All of the current modules are stopped,
    /// modules are updated, and the updated modules are restarted.
    ///
    /// Stops all modules, updates them, and then restarts.
    /// </summary>
    public class RestartPlanner : IPlanner
    {
        readonly ICommandFactory commandFactory;

        public RestartPlanner(ICommandFactory commandFactory)
        {
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
        }

        async public Task<Plan> PlanAsync(ModuleSet desired, ModuleSet current, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            Diff diff = desired.Diff(current);
            Plan plan = diff.IsEmpty
                ? Plan.Empty
                : await this.CreatePlan(desired, current, diff, runtimeInfo, moduleIdentities);

            return plan;
        }

        async Task<Plan> CreatePlan(ModuleSet desired, ModuleSet current, Diff diff, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            IEnumerable<Task<ICommand>> stopTasks = current.Modules.Select(m => this.commandFactory.StopAsync(m.Value));
            IEnumerable<ICommand> stop = await Task.WhenAll(stopTasks);
            IEnumerable<Task<ICommand>> removeTasks = diff.Removed.Select(name => this.commandFactory.RemoveAsync(current.Modules[name]));
            IEnumerable<ICommand> remove = await Task.WhenAll(removeTasks);
            IEnumerable<Task<ICommand>> startTasks = desired.Modules
                .Where(m => m.Value.DesiredStatus == ModuleStatus.Running)
                .Select(m => this.commandFactory.StartAsync(m.Value));
            IEnumerable<ICommand> start = await Task.WhenAll(startTasks);

            // Only update changed modules
            IList<Task<ICommand>> updateTasks = diff.Updated
                .Select(m => this.CreateOrUpdate(current, m, runtimeInfo, moduleIdentities))
                .ToList();
            IEnumerable<ICommand> update = await Task.WhenAll(updateTasks);

            IList<ICommand> commands = stop
                .Concat(remove)
                .Concat(update)
                .Concat(start).ToList();

            Events.PlanCreated(commands);
            return new Plan(commands);
        }

        public async Task<Plan> CreateShutdownPlanAsync(ModuleSet current)
        {
            IEnumerable<Task<ICommand>> stopTasks = current.Modules.Values
                .Where(c => !c.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase))
                .Select(m => this.commandFactory.StopAsync(m));
            ICommand[] stopCommands = await Task.WhenAll(stopTasks);
            ICommand parallelCommand = new ParallelGroupCommand(stopCommands);
            Events.ShutdownPlanCreated(stopCommands);
            return new Plan(new[] { parallelCommand });
        }

        async Task<ICommand> CreateOrUpdate(ModuleSet current, IModule desiredMod, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities) =>
            current.TryGetModule(desiredMod.Name, out IModule currentMod)
                ? await this.commandFactory.UpdateAsync(currentMod, new ModuleWithIdentity(desiredMod, moduleIdentities.GetValueOrDefault(desiredMod.Name)), runtimeInfo)
                : await this.commandFactory.CreateAsync(new ModuleWithIdentity(desiredMod, moduleIdentities.GetValueOrDefault(desiredMod.Name)), runtimeInfo);

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<RestartPlanner>();
            const int IdStart = AgentEventIds.RestartPlanner;

            enum EventIds
            {
                PlanCreated = IdStart,
            }

            public static void PlanCreated(IList<ICommand> commands)
            {
                Log.LogDebug((int)EventIds.PlanCreated, $"RestartPlanner created Plan, with {commands.Count} commands.");
            }

            public static void ShutdownPlanCreated(ICommand[] stopCommands)
            {
                Log.LogDebug((int)EventIds.PlanCreated, $"HealthRestartPlanner created shutdown Plan, with {stopCommands.Length} command(s).");
            }
        }
    }
}
