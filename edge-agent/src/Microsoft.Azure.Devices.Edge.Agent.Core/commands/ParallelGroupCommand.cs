// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ParallelGroupCommand : ICommand
    {
        readonly ICommand[] commandGroup;
        readonly Lazy<string> id;

        public ParallelGroupCommand(params ICommand[] group)
        {
            this.commandGroup = Preconditions.CheckNotNull(group, nameof(group));
            this.id = new Lazy<string>(() => this.commandGroup.Aggregate("", (prev, command) => command.Id + prev));
        }

        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        public Task ExecuteAsync(CancellationToken token)
        {
            IEnumerable<Task> executeCommands = this.commandGroup.Select(c => c.ExecuteAsync(token));
            return Task.WhenAll(executeCommands);
        }

        public Task UndoAsync(CancellationToken token)
        {
            IEnumerable<Task> undoCommands = this.commandGroup.Select(c => c.UndoAsync(token));
            return Task.WhenAll(undoCommands);
        }

        public string Show()
        {
            IEnumerable<string> commandDescriptions = this.commandGroup.Select(command => $"[{command.Show()}]");
            return $"Parallel Command Group: (\n  {string.Join("\n  ", commandDescriptions)}\n)";
        }

        public override string ToString() => this.Show();
    }
}
