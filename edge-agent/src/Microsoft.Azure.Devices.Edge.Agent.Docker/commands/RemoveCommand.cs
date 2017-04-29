// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RemoveCommand : ICommand
    {
        readonly IDockerClient client;
        readonly DockerModule module;

        public RemoveCommand(IDockerClient client, DockerModule module)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public Task ExecuteAsync(CancellationToken token)
        {
            var parameters = new ContainerRemoveParameters();
            return this.client.Containers.RemoveContainerAsync(this.module.Name, parameters);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker rm {this.module.Name}";
    }
}