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
        public readonly IKubernetes client;
        public readonly IConfigSource configSource;
        public readonly ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider;
        public readonly string iotHubHostname;
        public readonly string gatewayHostName;
        public readonly string deviceId;

        public KubernetesCommandFactory(string iotHubHostname, string gatewayHostName, string deviceId, IKubernetes client, IConfigSource configSource, ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
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
