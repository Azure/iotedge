// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class GroupCommand : ICommand
    {
        readonly ICommand[] commandGroup;
        readonly Lazy<string> id;

        public GroupCommand(params ICommand[] group)
        {
            this.commandGroup = Preconditions.CheckNotNull(group, nameof(group));
            this.id = new Lazy<string>(() => this.commandGroup.Aggregate("", (prev, command) => command.Id + prev));
        }

        // We use the sum of the IDs of the underlying commands as the id for this group
        // command.
        public string Id => this.id.Value;

        public async Task ExecuteAsync(CancellationToken token)
        {
            foreach (ICommand command in this.commandGroup)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                await command.ExecuteAsync(token);
            }
        }

        public async Task UndoAsync(CancellationToken token)
        {
            foreach (ICommand command in this.commandGroup)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                await command.UndoAsync(token);
            }
        }

        public string Show()
        {
            IEnumerable<string> commandDescriptions = this.commandGroup.Select(command => $"[{command.Show()}]");
            return $"Command Group: (\n  {string.Join("\n  ", commandDescriptions)}\n)";
        }

        public override string ToString() => this.Show();
    }
}
