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

        public async Task<Plan> PlanAsync(ModuleSet desired, ModuleSet current, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            Diff diff = desired.Diff(current);
            Plan plan = diff.IsEmpty
                ? Plan.Empty
                : await this.CreatePlan(desired, current, runtimeInfo, moduleIdentities);

            return plan;
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

        async Task<Plan> CreatePlan(ModuleSet desired, ModuleSet current, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            var commands = new List<ICommand>();

            // Create a grouping of desired and current modules based on their priority.
            // We want to process all the modules in the deployment (desired modules) and also include the modules
            // that are not specified in the deployment but are currently running on the device. This is so that
            // their processing is done in the right priority order.
            ILookup<uint, KeyValuePair<string, IModule>> desiredPriorityGroups = desired.Modules.ToLookup(x => x.Value.StartupOrder);
            ILookup<uint, KeyValuePair<string, IModule>> currentPriorityGroups = current.Modules.ToLookup(x => x.Value.StartupOrder);
            ImmutableSortedSet<uint> orderedPriorities = desiredPriorityGroups.Select(x => x.Key).Union(currentPriorityGroups.Select(x => x.Key)).ToImmutableSortedSet();
            var processedDesiredMatchingCurrentModules = new HashSet<string>();

            foreach (uint priority in orderedPriorities)
            {
                // The desired set is all the desired modules that have the priority of the current priority group being evaluated.
                ModuleSet priorityBasedDesiredSet = ModuleSet.Create(desiredPriorityGroups[priority].Select(x => x.Value).ToArray());

                // The current set is:
                // - All the current modules that correspond to the desired modules present in the current priority group.
                // - All the current modules that have the priority of the current priority group being evaluated which were not specified in the desired deployment config
                //   -and- have not already been processed yet.
                //   These are included so that they can be stopped and removed in the right priority order.
                IEnumerable<KeyValuePair<string, IModule>> desiredMatchingCurrentModules = current.Modules.Where(x => priorityBasedDesiredSet.Modules.ContainsKey(x.Key));
                ModuleSet priorityBasedCurrentSet = ModuleSet.Create(
                    desiredMatchingCurrentModules
                    .Union(currentPriorityGroups[priority].Where(x => !processedDesiredMatchingCurrentModules.Contains(x.Key)))
                    .Select(y => y.Value)
                    .ToArray());
                processedDesiredMatchingCurrentModules.UnionWith(desiredMatchingCurrentModules.Select(x => x.Key));

                commands.AddRange(await this.ProcessDesiredAndCurrentSets(priorityBasedDesiredSet, priorityBasedCurrentSet, runtimeInfo, moduleIdentities));
            }

            Events.PlanCreated(commands);
            return new Plan(commands);
        }

        async Task<IEnumerable<ICommand>> ProcessDesiredAndCurrentSets(
            ModuleSet desired, ModuleSet current, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            Diff diff = desired.Diff(current);
            IEnumerable<Task<ICommand>> stopTasks = current.Modules.Select(m => this.commandFactory.StopAsync(m.Value));
            IEnumerable<ICommand> stop = await Task.WhenAll(stopTasks);
            IEnumerable<Task<ICommand>> removeTasks = diff.Removed.Select(name => this.commandFactory.RemoveAsync(current.Modules[name]));
            IEnumerable<ICommand> remove = await Task.WhenAll(removeTasks);

            // Only update changed modules
            IList<Task<ICommand>> updateTasks = diff.AddedOrUpdated
                .Select(m => this.CreateOrUpdate(current, m, runtimeInfo, moduleIdentities))
                .ToList();
            IEnumerable<ICommand> update = await Task.WhenAll(updateTasks);

            IEnumerable<Task<ICommand>> startTasks = desired.Modules.Values
                .Where(m => m.DesiredStatus == ModuleStatus.Running)
                .Select(m => this.commandFactory.StartAsync(m));
            IEnumerable<ICommand> start = await Task.WhenAll(startTasks);

            return stop
                .Concat(remove)
                .Concat(update)
                .Concat(start);
        }

        async Task<ICommand> CreateOrUpdate(ModuleSet current, IModule desiredMod, IRuntimeInfo runtimeInfo, IImmutableDictionary<string, IModuleIdentity> moduleIdentities) =>
            current.TryGetModule(desiredMod.Name, out IModule currentMod)
                ? await this.commandFactory.UpdateAsync(currentMod, new ModuleWithIdentity(desiredMod, moduleIdentities.GetValueOrDefault(desiredMod.Name)), runtimeInfo)
                : await this.commandFactory.CreateAsync(new ModuleWithIdentity(desiredMod, moduleIdentities.GetValueOrDefault(desiredMod.Name)), runtimeInfo);

        static class Events
        {
            const int IdStart = AgentEventIds.RestartPlanner;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RestartPlanner>();

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
