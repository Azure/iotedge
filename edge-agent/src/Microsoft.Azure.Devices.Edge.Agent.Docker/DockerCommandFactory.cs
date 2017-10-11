// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
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

        public ICommand Create(IModuleWithIdentity module) =>
        module.Module is DockerModule
            ? new CreateCommand(this.client, (DockerModule) module.Module, module.ModuleIdentity, this.dockerLoggerConfig, this.configSource)
        : (ICommand)NullCommand.Instance;

        public ICommand Pull(IModule module) =>
            module is DockerModule
                ? new PullCommand(this.client, (DockerModule)module, this.FirstAuthConfigOrDefault((DockerModule)module))
                : (ICommand)NullCommand.Instance;

        public ICommand Update(IModule current, IModuleWithIdentity next) =>
            current is DockerModule && next.Module is DockerModule
                ? new UpdateCommand(this.client, (DockerModule)current, (DockerModule)next.Module, next.ModuleIdentity, this.dockerLoggerConfig, this.configSource)
                : (ICommand)NullCommand.Instance;

        public ICommand Remove(IModule module) =>
            module is DockerModule
                ? new RemoveCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Start(IModule module) =>
            module is DockerModule
                ? new StartCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Stop(IModule module) =>
            module is DockerModule
                ? new StopCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Restart(IModule module) =>
            module is DockerRuntimeModule
                ? new RestartCommand(this.client, (DockerRuntimeModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Wrap(ICommand command) => command;

        AuthConfig FirstAuthConfigOrDefault(DockerModule module)
        {
            var authConfigs = this.configSource.Configuration.GetSection("DockerRegistryAuth").Get<List<AuthConfig>>();
            return DockerUtil.FirstAuthConfigOrDefault(module.Config.Image, authConfigs);
        }
    }
}
