// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StartCommand : ICommand
    {
        readonly IDockerClient client;
        readonly DockerModule module;

        public StartCommand(IDockerClient client, DockerModule module)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public string Id => $"StartCommand({this.module.Name})";

        public Task ExecuteAsync(CancellationToken token)
        {
            var parameters = new ContainerStartParameters();
            return this.client.Containers.StartContainerAsync(this.module.Name, parameters, token);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker start {this.module.Name}";
    }
}
