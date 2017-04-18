// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

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

        public Plan Plan(ModuleSet desired, ModuleSet current)
        {
            IEnumerable<ICommand> stop = current.Modules.Select(m => this.commandFactory.Stop(m));
            IEnumerable<ICommand> start = desired.Modules.Select(m => this.commandFactory.Start(m));

            IList<ICommand> update = desired.Modules
                .Select(m => this.CreateOrUpdate(current, m))
                .ToList();

            IList<ICommand> commands = stop.Concat(update).Concat(start).ToList();
            return new Plan(commands);
        }

        ICommand CreateOrUpdate(ModuleSet current, IModule desiredMod) =>
            current.TryGetModule(desiredMod.Name, out IModule currentMod)
                ? this.commandFactory.Update(currentMod, desiredMod)
                : this.commandFactory.Create(desiredMod);
    }
}