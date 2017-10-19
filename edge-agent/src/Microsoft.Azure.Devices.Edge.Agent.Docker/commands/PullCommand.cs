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
    using System.Net;

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
            string[] imageParts = this.module.Config.Image.Split(':');            
            string image = imageParts[0];            
            string tag = imageParts.Length > 1 ? imageParts[1] : string.Empty;
            var pullParameters = new ImagesCreateParameters
            {
                FromImage = image,
                Tag = tag
            };

            try
            {
                await this.client.Images.CreateImageAsync(pullParameters,
                                                          this.authConfig,
                                                          new Progress<JSONMessage>(),
                                                          token);
            }
            catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)

            {
                throw new ImageNotFoundException(image, tag, ex.StatusCode.ToString(), ex);
            }
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker pull {this.module.Config.Image}";
    }
}
