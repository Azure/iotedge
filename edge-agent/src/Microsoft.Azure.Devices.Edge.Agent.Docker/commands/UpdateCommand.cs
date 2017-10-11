// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class UpdateCommand : ICommand
    {
        readonly ICommand remove;
        readonly ICommand create;

        public UpdateCommand(IDockerClient client, DockerModule current, DockerModule next, IModuleIdentity identity, DockerLoggingConfig dockerLoggerConfig, IConfigSource configSource)
        {
            Preconditions.CheckNotNull(client, nameof(client));
            Preconditions.CheckNotNull(current, nameof(current));
            Preconditions.CheckNotNull(next, nameof(next));
            Preconditions.CheckNotNull(dockerLoggerConfig, nameof(dockerLoggerConfig));

            this.remove = new RemoveCommand(client, current);
            this.create = new CreateCommand(client, next, identity, dockerLoggerConfig, configSource);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await this.remove.ExecuteAsync(token);
            await this.create.ExecuteAsync(token);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"{this.remove.Show()} && {this.create.Show()}";
    }
}
