// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class GroupCommand : ICommand
    {
        readonly ICommand[] commandGroup;

        public GroupCommand(params ICommand[] group)
        {
            this.commandGroup = Preconditions.CheckNotNull(group, nameof(group));
        }

        public static Task<ICommand> CreateAsync(params ICommand[] commandgroup)
        {
            return Task.FromResult(new GroupCommand(commandgroup) as ICommand);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            foreach(var command in this.commandGroup)
            {
                await command.ExecuteAsync(token);
            }
        }

        public async Task UndoAsync(CancellationToken token)
        {
            foreach (var command in this.commandGroup)
            {
                await command.UndoAsync(token);
            }
        }

        public string Show()
        {
            string showString = "Command Group: (";
            foreach (var command in this.commandGroup)
            {
                showString += "[" + command.Show() + "]";
            }
            showString += ")";
            return showString;
        }
    }
}
