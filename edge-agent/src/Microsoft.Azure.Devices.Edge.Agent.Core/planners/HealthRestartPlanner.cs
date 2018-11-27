// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Logging;
    using DiffState = System.ValueTuple<
        // added modules
        System.Collections.Generic.IList<IModule>,

        // updated modules because of deployment
        System.Collections.Generic.IList<IModule>,

        // update modules because runtime state changed
        System.Collections.Generic.IList<IRuntimeModule>,

        // removed modules
        System.Collections.Generic.IList<IRuntimeModule>,

        // modules that are running great
        System.Collections.Generic.IList<IRuntimeModule>
    >;
    using Newtonsoft.Json;

    public class HealthRestartPlanner : IPlanner
    {
        readonly ICommandFactory commandFactory;
        readonly IEntityStore<string, ModuleState> store;
        readonly TimeSpan intensiveCareTime;
        readonly IRestartPolicyManager restartManager;

        public HealthRestartPlanner(
            ICommandFactory commandFactory,
            IEntityStore<string, ModuleState> store,
            TimeSpan intensiveCareTime,
            IRestartPolicyManager restartManager
        )
        {
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.intensiveCareTime = intensiveCareTime;
            this.restartManager = Preconditions.CheckNotNull(restartManager, nameof(restartManager));
        }

        IEnumerable<Task<ICommand>> ApplyRestartPolicy(IEnumerable<IRuntimeModule> modules)
        {
            IEnumerable<IRuntimeModule> modulesToBeRestarted = this.restartManager.ApplyRestartPolicy(modules);
            IEnumerable<Task<ICommand>> restart = modulesToBeRestarted.Select(async module =>
            {
                ICommand group = new GroupCommand(
                    // restart the module
                    // await this.commandFactory.RestartAsync(module),

                    // TODO: Windows native containers have an outstanding bug where "docker restart"
                    // doesn't work. But a "docker stop" followed by a "docker start" will work. Putting
                    // in a temporary workaround to address this. This should be rolled back when the
                    // Windows bug is fixed.
                    await this.commandFactory.StopAsync(module),
                    await this.commandFactory.StartAsync(module),

                    // Update restart count and last restart time in store
                    await this.commandFactory.WrapAsync(
                        new AddToStoreCommand<ModuleState>(this.store, module.Name, new ModuleState(module.RestartCount + 1, DateTime.UtcNow))
                    )
                );

                return await this.commandFactory.WrapAsync(group);
            });
            return restart;
        }

        async Task<IEnumerable<ICommand>> ProcessAddedUpdatedModules(
            IList<IModule> modules,
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities,
            Func<IModuleWithIdentity, Task<ICommand>> createUpdateCommandMaker
        )
        {
            // new modules become a command group containing:
            //   create followed by a start command if the desired
            //   status is "running"
            var addedTasks = new List<Task<ICommand[]>>();
            foreach (IModule module in modules)
            {
                if (moduleIdentities.TryGetValue(module.Name, out IModuleIdentity moduleIdentity))
                {
                    var tasks = new List<Task<ICommand>>();
                    var moduleWithIdentity = new ModuleWithIdentity(module, moduleIdentity);
                    tasks.Add(createUpdateCommandMaker(moduleWithIdentity));
                    if (module.DesiredStatus == ModuleStatus.Running)
                    {
                        tasks.Add(this.commandFactory.StartAsync(module));
                    }

                    addedTasks.Add(Task.WhenAll(tasks));
                }
                else
                {
                    Events.UnableToProcessModule(module);
                }
            }

            // build GroupCommands from each command set
            IEnumerable<Task<ICommand>> commands = (await Task.WhenAll(addedTasks))
                .Select(cmds => this.commandFactory.WrapAsync(new GroupCommand(cmds)));

            return await Task.WhenAll(commands);
        }

        async Task<IEnumerable<ICommand>> ResetStatsForHealthyModulesAsync(IEnumerable<IRuntimeModule> modules)
        {
            // clear the "restartCount" and "lastRestartTime" values for running modules that have been up
            // for more than "IntensiveCareTime" & still have an entry for them in the store
            IList<ICommand> resetHealthStats = new List<ICommand>();
            foreach (IRuntimeModule module in modules)
            {
                if (await this.store.Contains(module.Name))
                {
                    // this value comes from docker; if this is equal to DateTime.MinValue then the container
                    // never exited before; if it is anything else then we check if it has been up for "IntensiveCareTime"
                    // and if yes, then we clear the health stats on it;
                    //
                    // the time returned by docker is in UTC timezone
                    if (module.LastExitTimeUtc != DateTime.MinValue && DateTime.UtcNow - module.LastExitTimeUtc > this.intensiveCareTime)
                    {
                        // NOTE: This is a "special" command in that it doesn't come from an "ICommandFactory". This
                        // command clears the health stats from the store.
                        resetHealthStats.Add(await this.commandFactory.WrapAsync(new RemoveFromStoreCommand<ModuleState>(this.store, module.Name)));
                        Events.ClearingRestartStats(module, this.intensiveCareTime);
                    }
                }
            }

            return resetHealthStats;
        }

        DiffState ProcessDiff(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);

            IList<IModule> added = diff.Updated.Where(m => !current.TryGetModule(m.Name, out _)).ToList();
            IList<IRuntimeModule> removed = diff.Removed.Select(name => (IRuntimeModule)current.Modules[name]).ToList();

            // We are interested in 2 kinds of "updated" modules:
            //
            //  [1] someone pushed a new deployment for this device that changed something
            //      for an existing module
            //  [2] something changed in the runtime state of the module - for example, it
            //      had a tragic untimely death
            //
            // We need to be able to distinguish between the two cases because for the latter
            // we want to apply the restart policy and for the former we want to simply
            // re-deploy.
            IList<IModule> updateDeployed = diff.Updated.Except(added).ToList(); // TODO: Should we do module name comparisons below instead of object comparisons? Faster?

            IList<IRuntimeModule> currentRuntimeModules = current.Modules.Values
                .Select(m => (IRuntimeModule)m)
                .Except(removed) // TODO: Should we do module name comparisons below instead of object comparisons? Faster?
                .Except(updateDeployed.Select(m => current.Modules[m.Name] as IRuntimeModule)).ToList();

            IList<IRuntimeModule> updateStateChanged = currentRuntimeModules
                .Where(m => m.DesiredStatus == ModuleStatus.Running && m.RuntimeStatus != ModuleStatus.Running).ToList();

            // Apart from all of the lists above, there can be modules in "current" where neither
            // the desired state has changed nor the runtime state has changed. For example, a module
            // that is expected to be "running" continues to run just fine. This won't show up in
            // any of the lists above. But we are still interested in these because we want to clear
            // the restart stats on them when they have been behaving well for "intensiveCareTime".
            //
            // Note that we are only interested in "running" modules. If there's a module that was
            // expected to be in the "stopped" state and continues to be in the "stopped" state, that
            // is not very interesting to us.
            IList<IRuntimeModule> runningGreat = currentRuntimeModules
                .Where(m => m.DesiredStatus == ModuleStatus.Running && m.RuntimeStatus == ModuleStatus.Running).ToList();

            return (added, updateDeployed, updateStateChanged, removed, runningGreat);
        }

        public async Task<Plan> PlanAsync(ModuleSet desired,
            ModuleSet current,
            IRuntimeInfo runtimeInfo,
            IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            Events.LogDesired(desired);
            Events.LogCurrent(current);
            // extract list of modules that need attention
            (IList<IModule> added, IList<IModule> updateDeployed, IList<IRuntimeModule> updateStateChanged, IList<IRuntimeModule> removed, IList<IRuntimeModule> runningGreat) = this.ProcessDiff(desired, current);

            List<ICommand> updateRuntimeCommands = await this.GetUpdateRuntimeCommands(updateDeployed, moduleIdentities, runtimeInfo);

            // create "stop" commands for modules that have been updated/removed
            IEnumerable<Task<ICommand>> stopTasks = updateDeployed
                .Concat(removed)
                .Select(m => this.commandFactory.StopAsync(m));
            IEnumerable<ICommand> stop = await Task.WhenAll(stopTasks);

            // create "remove" commands for modules that are being deleted in this deployment
            IEnumerable<Task<ICommand>> removeTasks = removed.Select(m => this.commandFactory.RemoveAsync(m));
            IEnumerable<ICommand> remove = await Task.WhenAll(removeTasks);

            // remove any saved state we might have for modules that are being removed or
            // are being updated because of a deployment
            IEnumerable<Task<ICommand>> removeStateTasks = removed
                .Concat(updateDeployed)
                .Select(m => this.commandFactory.WrapAsync(new RemoveFromStoreCommand<ModuleState>(this.store, m.Name)));
            IEnumerable<ICommand> removeState = await Task.WhenAll(removeStateTasks);

            // create pull, create, update and start commands for added/updated modules
            IEnumerable<ICommand> addedCommands = await this.ProcessAddedUpdatedModules(
                added,
                moduleIdentities,
                m => this.commandFactory.CreateAsync(m, runtimeInfo)
            );

            IEnumerable<ICommand> updatedCommands = await this.ProcessAddedUpdatedModules(
                updateDeployed,
                moduleIdentities,
                m =>
                {
                    current.TryGetModule(m.Module.Name, out IModule currentModule);
                    return this.commandFactory.UpdateAsync(
                        currentModule,
                        m,
                        runtimeInfo);
                }
            );

            // apply restart policy for modules that are not in the deployment list and aren't running
            IEnumerable<Task<ICommand>> restartTasks = this.ApplyRestartPolicy(updateStateChanged.Where(m => !m.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase)));
            IEnumerable<ICommand> restart = await Task.WhenAll(restartTasks);

            // clear the "restartCount" and "lastRestartTime" values for running modules that have been up
            // for more than "IntensiveCareTime" & still have an entry for them in the store
            IEnumerable<ICommand> resetHealthStatus = await this.ResetStatsForHealthyModulesAsync(runningGreat);

            IList<ICommand> commands = updateRuntimeCommands
                .Concat(stop)
                .Concat(remove)
                .Concat(removeState)
                .Concat(addedCommands)
                .Concat(updatedCommands)
                .Concat(restart)
                .Concat(resetHealthStatus)
                .ToList();

            Events.PlanCreated(commands);
            return new Plan(commands);
        }

        async Task<List<ICommand>> GetUpdateRuntimeCommands(IList<IModule> updateDeployed, IImmutableDictionary<string, IModuleIdentity> moduleIdentities, IRuntimeInfo runtimeInfo)
        {
            var updateRuntimeCommands = new List<ICommand>();
            IModule edgeAgentModule = updateDeployed.FirstOrDefault(m => m.Name.Equals(Constants.EdgeAgentModuleName, StringComparison.OrdinalIgnoreCase));
            if (edgeAgentModule != null)
            {
                if (moduleIdentities.TryGetValue(edgeAgentModule.Name, out IModuleIdentity edgeAgentIdentity))
                {
                    updateDeployed.Remove(edgeAgentModule);
                    ICommand updateEdgeAgentCommand = await this.commandFactory.UpdateEdgeAgentAsync(new ModuleWithIdentity(edgeAgentModule, edgeAgentIdentity), runtimeInfo);
                    updateRuntimeCommands.Add(updateEdgeAgentCommand);
                }
                else
                {
                    Events.UnableToUpdateEdgeAgent();
                }
            }

            return updateRuntimeCommands;
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
    }

    static class Events
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<HealthRestartPlanner>();
        const int IdStart = AgentEventIds.HealthRestartPlanner;

        enum EventIds
        {
            PlanCreated = IdStart,
            ClearRestartStats,
            DesiredModules,
            CurrentModules,
            UnableToUpdateEdgeAgent,
            UnableToProcessModule
        }

        public static void PlanCreated(IList<ICommand> commands)
        {
            Log.LogDebug((int)EventIds.PlanCreated, $"HealthRestartPlanner created Plan, with {commands.Count} command(s).");
        }

        public static void ClearingRestartStats(IRuntimeModule module, TimeSpan intensiveCareTime)
        {
            Log.LogInformation((int)EventIds.ClearRestartStats, $"HealthRestartPlanner is clearing restart stats for module '{module.Name}' as it has been running healthy for {intensiveCareTime}.");
        }

        internal static void LogDesired(ModuleSet desired)
        {
            IDictionary<string, IModule> modules = desired.Modules.ToImmutableDictionary();
            Log.LogDebug((int)EventIds.DesiredModules, $"List of desired modules is - {JsonConvert.SerializeObject(modules)}");
        }

        internal static void LogCurrent(ModuleSet current)
        {
            IDictionary<string, IModule> modules = current.Modules.ToImmutableDictionary();
            Log.LogDebug((int)EventIds.CurrentModules, $"List of current modules is - {JsonConvert.SerializeObject(modules)}");
        }

        public static void UnableToUpdateEdgeAgent()
        {
            Log.LogInformation((int)EventIds.UnableToUpdateEdgeAgent, $"Unable to update EdgeAgent module as the EdgeAgent module identity could not be obtained");
        }

        public static void UnableToProcessModule(IModule module)
        {
            Log.LogInformation((int)EventIds.UnableToProcessModule, $"Unable to process module {module.Name} add or update as the module identity could not be obtained");
        }

        public static void ShutdownPlanCreated(ICommand[] stopCommands)
        {
            Log.LogDebug((int)EventIds.PlanCreated, $"HealthRestartPlanner created shutdown Plan, with {stopCommands.Length} command(s).");
        }
    }
}
