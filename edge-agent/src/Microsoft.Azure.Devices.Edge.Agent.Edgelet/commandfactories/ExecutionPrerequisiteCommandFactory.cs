// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.CommandFactories
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands;

    // CommandFactory that will issue ExecutionPrerequisiteException on a
    // failed image pull.
    public class ExecutionPrerequisiteCommandFactory : ICommandFactory
    {
        ICommandFactory commandFactory;

        public ExecutionPrerequisiteCommandFactory(ICommandFactory commandFactory)
        {
            this.commandFactory = commandFactory;
        }

        public Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            return this.commandFactory.UpdateEdgeAgentAsync(module, runtimeInfo);
        }

        public Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            return this.commandFactory.CreateAsync(module, runtimeInfo);
        }

        public async Task<ICommand> PrepareUpdateAsync(IModule module, IRuntimeInfo runtimeInfo)
        {
            ICommand prepareUpdate = await this.commandFactory.PrepareUpdateAsync(module, runtimeInfo);
            return new ExecutionPrerequisiteCommand(prepareUpdate);
        }

        public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            return this.commandFactory.UpdateAsync(current, next, runtimeInfo);
        }

        public Task<ICommand> RemoveAsync(IModule module)
        {
            return this.commandFactory.RemoveAsync(module);
        }

        public Task<ICommand> StartAsync(IModule module)
        {
            return this.commandFactory.StartAsync(module);
        }

        public Task<ICommand> RestartAsync(IModule module)
        {
            return this.commandFactory.RestartAsync(module);
        }

        public Task<ICommand> StopAsync(IModule module)
        {
            return this.commandFactory.StopAsync(module);
        }

        public Task<ICommand> WrapAsync(ICommand command)
        {
            return this.commandFactory.WrapAsync(command);
        }
    }
}
