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
        readonly IRuntimeModule module;

        public RestartCommand(IDockerClient client, IRuntimeModule module)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public string Id => $"RestartCommand({this.module.Name})";

        public Task ExecuteAsync(CancellationToken token) => this.client.Containers.RestartContainerAsync(this.module.Name, new ContainerRestartParameters(), token);

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public string Show() => $"docker restart {this.module.Name} [Restart attempt #{this.module.RestartCount + 1}]";
    }
}
