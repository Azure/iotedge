// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DockerCommandFactory : ICommandFactory
    {
        readonly IDockerClient client;
        readonly DockerLoggingConfig dockerLoggerConfig;
        readonly IConfigSource configSource;
        readonly ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider;

        public DockerCommandFactory(IDockerClient dockerClient, DockerLoggingConfig dockerLoggingConfig, IConfigSource configSource, ICombinedConfigProvider<CombinedDockerConfig> combinedConfigProvider)
        {
            this.client = Preconditions.CheckNotNull(dockerClient, nameof(dockerClient));
            this.dockerLoggerConfig = Preconditions.CheckNotNull(dockerLoggingConfig, nameof(dockerLoggingConfig));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
        }

        public Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) => Task.FromResult(NullCommand.Instance as ICommand);

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            if (module.Module is DockerModule dockerModule)
            {
                CombinedDockerConfig combinedDockerConfig = this.combinedConfigProvider.GetCombinedConfig(dockerModule, runtimeInfo);

                var commands = new List<ICommand>();
                if (module.Module.ImagePullPolicy != ImagePullPolicy.Never)
                {
                    commands.Add(new PullCommand(this.client, combinedDockerConfig));
                }

                commands.Add(await CreateCommand.BuildAsync(this.client, dockerModule, module.ModuleIdentity, this.dockerLoggerConfig, this.configSource, module.Module is EdgeHubDockerModule));
                return new GroupCommand(commands.ToArray());
            }

            return NullCommand.Instance;
        }

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo)
        {
            if (current is DockerModule currentDockerModule && next.Module is DockerModule nextDockerModule)
            {
                CombinedDockerConfig combinedDockerConfig = this.combinedConfigProvider.GetCombinedConfig(nextDockerModule, runtimeInfo);
                var commands = new List<ICommand>();
                if (next.Module.ImagePullPolicy != ImagePullPolicy.Never)
                {
                    commands.Add(new PullCommand(this.client, combinedDockerConfig));
                }

                commands.AddRange(
                    new ICommand[]
                    {
                        new StopCommand(this.client, currentDockerModule),
                        new RemoveCommand(this.client, currentDockerModule),
                        await CreateCommand.BuildAsync(this.client, nextDockerModule, next.ModuleIdentity, this.dockerLoggerConfig, this.configSource, next.Module is EdgeHubDockerModule)
                    });
                return new GroupCommand(commands.ToArray());
            }

            return NullCommand.Instance;
        }

        public Task<ICommand> RemoveAsync(IModule module) =>
            Task.FromResult(
                module is DockerModule
                    ? new RemoveCommand(this.client, (DockerModule)module)
                    : (ICommand)NullCommand.Instance);

        public Task<ICommand> StartAsync(IModule module) =>
            Task.FromResult(
                module is DockerModule
                    ? new StartCommand(this.client, (DockerModule)module)
                    : (ICommand)NullCommand.Instance);

        public Task<ICommand> StopAsync(IModule module) =>
            Task.FromResult(
                module is DockerModule
                    ? new StopCommand(this.client, (DockerModule)module)
                    : (ICommand)NullCommand.Instance);

        public Task<ICommand> RestartAsync(IModule module) =>
            Task.FromResult(
                module is DockerRuntimeModule
                    ? new RestartCommand(this.client, (DockerRuntimeModule)module)
                    : (ICommand)NullCommand.Instance);

        public Task<ICommand> WrapAsync(ICommand command) => Task.FromResult(command);
    }
}
