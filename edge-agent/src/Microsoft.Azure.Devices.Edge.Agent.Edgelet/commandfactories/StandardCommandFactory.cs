// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.CommandFactories
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands;

    // Create / update commands needs to be grouped so that
    // image pull is guaranteed to succeed before the container
    // is actually created / updated. This prevents multiple
    // create commands from getting executed within aziot-edged
    // if EdgeAgent timesout create request and reissues.
    //
    // Multiple create requests being executed within
    // aziot-edged will lead to race condition with workload
    // socket creation and removal.
    public class StandardCommandFactory : ICommandFactory
    {
        ICommandFactory commandFactory;
        ICommandFactory nullCommandFactory;

        public StandardCommandFactory(ICommandFactory commandFactory)
        {
            this.commandFactory = commandFactory;
            this.nullCommandFactory = NullCommandFactory.Instance;
        }

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            ICommand prepareUpdate = await this.commandFactory.PrepareUpdateAsync(module.Module, runtimeInfo);
            ICommand updateEdgeAgent = await this.commandFactory.UpdateEdgeAgentAsync(module, runtimeInfo);
            return new GroupCommand(prepareUpdate, updateEdgeAgent);
        }

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> prepareUpdate = this.commandFactory.PrepareUpdateAsync(module.Module, runtimeInfo);
            Task<ICommand> create = this.commandFactory.CreateAsync(module, runtimeInfo);

            IList<Task<ICommand>> cmds = new List<Task<ICommand>> { prepareUpdate, create };
            return await this.commandFactory.WrapAsync(new GroupCommand(await Task.WhenAll(cmds)));
        }

        public Task<ICommand> PrepareUpdateAsync(IModule module, IRuntimeInfo runtimeInfo)
        {
            return this.nullCommandFactory.PrepareUpdateAsync(module, runtimeInfo);
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> prepareUpdate = this.commandFactory.PrepareUpdateAsync(next.Module, runtimeInfo);
            Task<ICommand> update = this.commandFactory.UpdateAsync(current, next, runtimeInfo);

            IList<Task<ICommand>> cmds = new List<Task<ICommand>> { prepareUpdate, update };
            return await this.commandFactory.WrapAsync(new GroupCommand(await Task.WhenAll(cmds)));
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
