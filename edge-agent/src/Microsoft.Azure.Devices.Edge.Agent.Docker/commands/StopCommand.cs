// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StopCommand : ICommand
    {
        static readonly TimeSpan WaitBeforeKill = TimeSpan.FromSeconds(10);
        readonly IDockerClient client;
        readonly DockerModule module;

        public StopCommand(IDockerClient client, DockerModule module)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public Task ExecuteAsync(CancellationToken token)
        {
            var parameters = new ContainerStopParameters
            {
                WaitBeforeKillSeconds = (uint)WaitBeforeKill.TotalSeconds
            };
            return this.client.Containers.StopContainerAsync(this.module.Name, parameters, token);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker stop -t {(uint)WaitBeforeKill.TotalSeconds} {this.module.Name}";
    }
}