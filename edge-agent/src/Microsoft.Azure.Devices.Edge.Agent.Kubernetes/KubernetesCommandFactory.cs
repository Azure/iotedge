// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
    {
        using System.Threading.Tasks;
        using k8s;
        using global::Docker.DotNet;
        using Microsoft.Azure.Devices.Edge.Agent.Core;
        using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
        using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Commands;
        using Microsoft.Azure.Devices.Edge.Util;
        using Microsoft.Azure.Devices.Edge.Agent.Docker;

        public class KubernetesCommandFactory : ICommandFactory
        {
            public KubernetesCommandFactory()
            {
            }

            public Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) => Task.FromResult(NullCommand.Instance as ICommand);

            public Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) =>
                Task.FromResult((ICommand)NullCommand.Instance);

            public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo) =>
                Task.FromResult((ICommand)NullCommand.Instance);

            public Task<ICommand> RemoveAsync(IModule module) =>
                Task.FromResult((ICommand)NullCommand.Instance);

            public Task<ICommand> StartAsync(IModule module) =>
                Task.FromResult((ICommand)NullCommand.Instance);

            public Task<ICommand> StopAsync(IModule module) =>
                Task.FromResult((ICommand)NullCommand.Instance);

            public Task<ICommand> RestartAsync(IModule module) =>
                Task.FromResult((ICommand)NullCommand.Instance);

            public Task<ICommand> WrapAsync(ICommand command) => Task.FromResult(command);
        }
    }
