// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;

    public class TestCommandFactory : ICommandFactory
    {
        public IList<(string, string)> RecordedCommands { get; }

        public TestCommandFactory()
        {
            this.RecordedCommands = new List<(string, string)>();
        }

        public Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            this.RecordedCommands.Add(("create", module.Module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            this.RecordedCommands.Add(("update", next.Module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            this.RecordedCommands.Add(("update", module.Module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> RemoveAsync(IModule module)
        {
            this.RecordedCommands.Add(("remove", module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> StartAsync(IModule module)
        {
            this.RecordedCommands.Add(("start", module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> StopAsync(IModule module)
        {
            this.RecordedCommands.Add(("stop", module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> RestartAsync(IModule module)
        {
            this.RecordedCommands.Add(("restart", module.Name));
            return Task.FromResult(NullCommand.Instance as ICommand);
        }

        public Task<ICommand> WrapAsync(ICommand command)
        {
            return Task.FromResult(NullCommand.Instance as ICommand);
        }
    }
}
