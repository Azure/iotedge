// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunner;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ExecutionPrerequisiteCommand : ICommand
    {
        readonly ICommand innerCommand;

        public ExecutionPrerequisiteCommand(ICommand command)
        {
            this.innerCommand = command;
        }

        public string Id => $"ExecutionPrerequisiteCommand({this.innerCommand.Id})";

        public string Show() => $"{this.innerCommand.Show()}";

        public async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                await this.innerCommand.ExecuteAsync(token);
            }
            catch (Exception e)
            {
                throw new ExecutionPrerequisiteException("Failed to execute ExecutionPrerequisite command", e);
            }
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;
    }
}
