// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;

    public interface ICommandFactory
    {
        Task<ICommand> UpdateEdgeAgentAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo);

        Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo);

        Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo);

        Task<ICommand> RemoveAsync(IModule module);

        Task<ICommand> StartAsync(IModule module);

        Task<ICommand> RestartAsync(IModule module);

        Task<ICommand> StopAsync(IModule module);

        Task<ICommand> WrapAsync(ICommand command);
    }
}
