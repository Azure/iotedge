// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;

    public interface ICommandFactory
    {
        Task<ICommand> CreateAsync(IModuleWithIdentity module);

        Task<ICommand> PullAsync(IModule module);

        Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next);

        Task<ICommand> RemoveAsync(IModule module);

        Task<ICommand> StartAsync(IModule module);

        Task<ICommand> RestartAsync(IModule module);

        Task<ICommand> StopAsync(IModule module);

        Task<ICommand> WrapAsync(ICommand command);
    }
}
