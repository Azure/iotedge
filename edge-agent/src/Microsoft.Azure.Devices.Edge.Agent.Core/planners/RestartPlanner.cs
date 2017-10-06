// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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

        public Task<Plan> PlanAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            Plan plan = diff.IsEmpty
                ? Core.Plan.Empty
                : this.CreatePlan(desired, current, diff);

            return Task.FromResult(plan);
        }

        Plan CreatePlan(ModuleSet desired, ModuleSet current, Diff diff)
        {
            IEnumerable<ICommand> stop = current.Modules.Select(m => this.commandFactory.Stop(m.Value));
            IEnumerable<ICommand> remove = diff.Removed.Select(name => this.commandFactory.Remove(current.Modules[name]));
            IEnumerable<ICommand> start = desired.Modules
                .Where(m => m.Value.DesiredStatus == ModuleStatus.Running)
                .Select(m => this.commandFactory.Start(m.Value));

            IList<ICommand> pull = desired.Modules
                .Select(m => this.commandFactory.Pull(m.Value))
                .ToList();

            // Only update changed modules
            IList<ICommand> update = diff.Updated
                .Select(m => this.CreateOrUpdate(current, m))
                .ToList();

            IList<ICommand> commands = stop
                .Concat(remove)
                .Concat(pull)
                .Concat(update)
                .Concat(start).ToList();

            Events.PlanCreated(commands);
            return new Plan(commands);
        }

        ICommand CreateOrUpdate(ModuleSet current, IModule desiredMod) =>
            current.TryGetModule(desiredMod.Name, out IModule currentMod)
                ? this.commandFactory.Update(currentMod, desiredMod)
                : this.commandFactory.Create(desiredMod);

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
        }
    }
}