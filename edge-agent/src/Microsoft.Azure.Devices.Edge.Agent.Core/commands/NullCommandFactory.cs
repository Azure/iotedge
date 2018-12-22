// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading.Tasks;

    public class NullCommandFactory : ICommandFactory
    {
        public static NullCommandFactory Instance { get; } = new NullCommandFactory();

        NullCommandFactory()
        {
        }

        public Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> RemoveAsync(IModule module) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> StartAsync(IModule module) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> StopAsync(IModule module) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> RestartAsync(IModule module) => Task.FromResult<ICommand>(NullCommand.Instance);

        public Task<ICommand> WrapAsync(ICommand command) => Task.FromResult(command);
    }
}
