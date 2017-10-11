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
        System.Collections.Generic.IEnumerable<IModule>,

        // updated modules because of deployment
        System.Collections.Generic.IEnumerable<IModule>,

        // update modules because runtime state changed
        System.Collections.Generic.IEnumerable<IRuntimeModule>,

        // removed modules
        System.Collections.Generic.IEnumerable<IRuntimeModule>,

        // modules that are running great
        System.Collections.Generic.IEnumerable<IRuntimeModule>
    >;

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

        IEnumerable<ICommand> ApplyRestartPolicy(IEnumerable<IRuntimeModule> modules)
        {
            IEnumerable<IRuntimeModule> modulesToBeRestarted = this.restartManager.ApplyRestartPolicy(modules);
            IEnumerable<ICommand> restart = modulesToBeRestarted.SelectMany(module => new ICommand[]
            {
                // restart the module
                this.commandFactory.Restart(module),

                // Update restart count and last restart time in store
                this.commandFactory.Wrap(
                    new UpdateModuleStateCommand(
                        module, this.store, new ModuleState(module.RestartCount + 1, DateTime.UtcNow)
                    )
                )
            }).ToArray();

            return restart;
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
                        resetHealthStats.Add(this.commandFactory.Wrap(new RemoveModuleStateCommand(module, this.store)));
                        Events.ClearedRestartStats(module, this.intensiveCareTime);
                    }
                }
            }

            return resetHealthStats;
        }

        DiffState ProcessDiff(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);

            IEnumerable<IModule> added = diff.Updated.Where(m => !current.TryGetModule(m.Name, out _));
            IEnumerable<IRuntimeModule> removed = diff.Removed.Select(name => (IRuntimeModule)current.Modules[name]);

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
            IEnumerable<IModule> updateDeployed = diff.Updated.Except(added); // TODO: Should we do module name comparisons below instead of object comparisons? Faster?

            IEnumerable<IRuntimeModule> currentRuntimeModules = current.Modules.Values
                .Select(m => (IRuntimeModule)m)
                .Except(removed) // TODO: Should we do module name comparisons below instead of object comparisons? Faster?
                .Except(updateDeployed.Select(m => current.Modules[m.Name] as IRuntimeModule));

            IEnumerable<IRuntimeModule> updateStateChanged = currentRuntimeModules
                .Where(m => m.DesiredStatus == ModuleStatus.Running && m.RuntimeStatus != ModuleStatus.Running);

            // Apart from all of the lists above, there can be modules in "current" where neither
            // the desired state has changed nor the runtime state has changed. For example, a module
            // that is expected to be "running" continues to run just fine. This won't show up in
            // any of the lists above. But we are still interested in these because we want to clear
            // the restart stats on them when they have been behaving well for "intensiveCareTime".
            //
            // Note that we are only interested in "running" modules. If there's a module that was
            // expected to be in the "stopped" state and continues to be in the "stopped" state, that
            // is not very interesting to us.
            IEnumerable<IRuntimeModule> runningGreat = currentRuntimeModules
                .Where(m => m.DesiredStatus == ModuleStatus.Running && m.RuntimeStatus == ModuleStatus.Running);

            return (added, updateDeployed, updateStateChanged, removed, runningGreat);
        }

        public async Task<Plan> PlanAsync(ModuleSet desired, ModuleSet current, IImmutableDictionary<string, IModuleIdentity> moduleIdentities)
        {
            // extract list of modules that need attention
            var (added, updateDeployed, updateStateChanged, removed, runningGreat) = this.ProcessDiff(desired, current);
            var modulesAddedOrUpdated = added.Concat(updateDeployed);

            // create "stop" commands for modules that have been updated/removed
            IEnumerable<ICommand> stop = updateDeployed
                .Concat(removed)
                .Select(m => this.commandFactory.Stop(m));

            // create "remove" commands for modules that are being deleted in this deployment
            IEnumerable<ICommand> remove = removed.Select(m => this.commandFactory.Remove(m));

            // remove any saved state we might for moduels that are being removed
            IEnumerable<ICommand> removeState = removed.Select(m => this.commandFactory.Wrap(new RemoveModuleStateCommand(m, this.store)));

            // create "pull" commands for modules that have been added/updated
            IEnumerable<ICommand> pull = modulesAddedOrUpdated.Select(m => this.commandFactory.Pull(m));

            // create "create" commands for modules that have been added
            IEnumerable<ICommand> create = added
                .Select(m => this.commandFactory.Create(new ModuleWithIdentity(m, moduleIdentities.GetValueOrDefault(m.Name))));

            // create "update" commands for modules that have been updated
            IEnumerable<ICommand> update = updateDeployed
                .Select(m =>
                {
                    current.TryGetModule(m.Name, out IModule currentModule);
                    return this.commandFactory.Update(currentModule, new ModuleWithIdentity(m, moduleIdentities.GetValueOrDefault(m.Name)));
                });

            // create "start" commands for modules that have been added/updated and where the
            // status desired is "Running"; this handles the case where someone adds/updates a
            // module to the deployment but has the desired "status" field as "Stopped"
            IEnumerable<ICommand> start = modulesAddedOrUpdated
                .Where(m => m.DesiredStatus == ModuleStatus.Running)
                .Select(m => this.commandFactory.Start(m));

            // apply restart policy for modules that are not in the deployment list and aren't running
            IEnumerable<ICommand> restart = this.ApplyRestartPolicy(updateStateChanged);

            // clear the "restartCount" and "lastRestartTime" values for running modules that have been up
            // for more than "IntensiveCareTime" & still have an entry for them in the store
            IEnumerable<ICommand> resetHealthStatus = await this.ResetStatsForHealthyModulesAsync(runningGreat);

            IList<ICommand> commands = stop
                .Concat(remove)
                .Concat(removeState)
                .Concat(pull)
                .Concat(create)
                .Concat(update)
                .Concat(start)
                .Concat(restart)
                .Concat(resetHealthStatus)
                .ToList();

            Events.PlanCreated(commands);
            return new Core.Plan(commands);
        }
    }

    static class Events
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<HealthRestartPlanner>();
        const int IdStart = AgentEventIds.HealthRestartPlanner;

        enum EventIds
        {
            PlanCreated = IdStart,
            ScheduledModule = IdStart + 2,
            ClearRestartStats = IdStart + 3
        }

        public static void PlanCreated(IList<ICommand> commands)
        {
            Log.LogDebug((int)EventIds.PlanCreated, $"HealthRestartPlanner created Plan, with {commands.Count} commands.");
        }

        public static void ClearedRestartStats(IRuntimeModule module, TimeSpan intensiveCareTime)
        {
            Log.LogInformation((int)EventIds.ClearRestartStats, $"HealthRestartPlanner cleared restart stats for module '{module.Name}' as it has been running healthy for {intensiveCareTime}.");
        }
    }
}
