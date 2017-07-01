// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PullCommand : ICommand
    {
        const int BufferSize = 1 << 19; // 0.5 MB
        readonly IDockerClient client;
        readonly DockerModule module;
        readonly AuthConfig authConfig;

        public PullCommand(IDockerClient client, DockerModule module, AuthConfig authConfig)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.authConfig = authConfig;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var pullParameters = new ImagesPullParameters
            {
                Parent = this.module.Config.Image,
                Tag = this.module.Config.Tag
            };
            Stream stream = await this.client.Images.PullImageAsync(pullParameters, this.authConfig);

            // We need to read the entire contents of the stream to ensure the image is fully downloaded
            await stream.CopyToAsync(Stream.Null, BufferSize, token);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker pull {this.module.Config.Image}:{this.module.Config.Tag}";
    }
}