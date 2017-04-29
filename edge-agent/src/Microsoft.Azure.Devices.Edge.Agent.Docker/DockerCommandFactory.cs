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

        public DockerCommandFactory(IDockerClient client)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
        }

        public ICommand Create(IModule module) =>
            module is DockerModule
                ? new CreateCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Pull(IModule module) =>
            module is DockerModule
                ? new PullCommand(this.client, (DockerModule)module)
                : (ICommand)NullCommand.Instance;

        public ICommand Update(IModule current, IModule next) =>
            current is DockerModule && next is DockerModule
                ? new UpdateCommand(this.client, (DockerModule)current, (DockerModule)next)
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