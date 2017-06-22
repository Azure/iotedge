// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
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

        public DockerCommandFactory(IDockerClient dockerClient, DockerLoggingConfig dockerLoggingConfig, IConfigSource configSource)
        {
            this.client = Preconditions.CheckNotNull(dockerClient, nameof(dockerClient));
            this.dockerLoggerConfig = Preconditions.CheckNotNull(dockerLoggingConfig, nameof(dockerLoggingConfig));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
        }

        public ICommand Create(IModule module) =>
            module is DockerModule
                ? new CreateCommand(this.client, (DockerModule)module, this.dockerLoggerConfig, this.configSource)
                : (ICommand)NullCommand.Instance;

        public ICommand Pull(IModule module) =>
            module is DockerModule
                ? new PullCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Update(IModule current, IModule next) =>
            current is DockerModule && next is DockerModule
                ? new UpdateCommand(this.client, (DockerModule)current, (DockerModule)next, this.dockerLoggerConfig, this.configSource)
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
    }
}