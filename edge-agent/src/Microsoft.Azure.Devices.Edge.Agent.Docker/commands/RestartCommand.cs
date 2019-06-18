// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RestartCommand : ICommand
    {
        readonly IDockerClient client;
        readonly string id;

        public RestartCommand(IDockerClient client, string id)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
        }

        public string Id => $"RestartCommand({this.id})";

        public Task ExecuteAsync(CancellationToken token) => this.client.Containers.RestartContainerAsync(this.id, new ContainerRestartParameters(), token);

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public string Show() => $"docker restart {this.id}";
    }
}
