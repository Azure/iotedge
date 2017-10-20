// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class DockerCommandFactory : ICommandFactory
    {
        readonly IDockerClient client;
        readonly DockerLoggingConfig dockerLoggerConfig;
        readonly IConfigSource configSource;

        public DockerCommandFactory(IDockerClient dockerClient, DockerLoggingConfig dockerLoggingConfig, IConfigSource configSource)
        {
            this.client = Preconditions.CheckNotNull(dockerClient, nameof(dockerClient));
            this.dockerLoggerConfig = Preconditions.CheckNotNull(dockerLoggingConfig, nameof(dockerLoggingConfig));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
        }

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module) =>
            module.Module is DockerModule
                ? await CreateCommand.BuildAsync(this.client, (DockerModule)module.Module, module.ModuleIdentity, this.dockerLoggerConfig, this.configSource, module.Module is EdgeHubDockerModule)
                : NullCommand.Instance;

        public Task<ICommand> PullAsync(IModule module) =>
            Task.FromResult(module is DockerModule
                ? new PullCommand(this.client, (DockerModule)module, this.FirstAuthConfigOrDefault((DockerModule)module))
                : (ICommand)NullCommand.Instance);

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next) =>
            current is DockerModule && next.Module is DockerModule
                ? await UpdateCommand.BuildAsync(this.client, (DockerModule)current, (DockerModule)next.Module, next.ModuleIdentity, this.dockerLoggerConfig, this.configSource)
                : NullCommand.Instance;

        public Task<ICommand> RemoveAsync(IModule module) =>
            Task.FromResult(module is DockerModule
                ? new RemoveCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance);

        public Task<ICommand> StartAsync(IModule module) =>
            Task.FromResult(module is DockerModule
                ? new StartCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance);

        public Task<ICommand> StopAsync(IModule module) =>
            Task.FromResult(module is DockerModule
                ? new StopCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance);

        public Task<ICommand> RestartAsync(IModule module) =>
            Task.FromResult(module is DockerRuntimeModule
                ? new RestartCommand(this.client, (DockerRuntimeModule)module)
                : (ICommand)NullCommand.Instance);

        public Task<ICommand> WrapAsync(ICommand command) => Task.FromResult(command);

        AuthConfig FirstAuthConfigOrDefault(DockerModule module)
        {
            var authConfigs = this.configSource.Configuration.GetSection("DockerRegistryAuth").Get<List<AuthConfig>>();
            return DockerUtil.FirstAuthConfigOrDefault(module.Config.Image, authConfigs);
        }
    }
}
