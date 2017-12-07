// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System;
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

        public static Task<ICommand> CreateAsync(params ICommand[] commandgroup)
        {
            return Task.FromResult(new GroupCommand(commandgroup) as ICommand);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            foreach (ICommand command in this.commandGroup)
            {
                await command.ExecuteAsync(token);
            }
        }

        public async Task UndoAsync(CancellationToken token)
        {
            foreach (ICommand command in this.commandGroup)
            {
                await command.UndoAsync(token);
            }
        }

        public string Show()
        {
            string showString = "Command Group: (";
            foreach (ICommand command in this.commandGroup)
            {
                showString += "[" + command.Show() + "]";
            }
            showString += ")";
            return showString;
        }
    }
}
