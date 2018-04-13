// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Net;

    public class PullCommand : ICommand
    {
        readonly IDockerClient client;
        readonly CombinedDockerConfig combinedDockerConfig;

        public PullCommand(IDockerClient client, CombinedDockerConfig combinedDockerConfig)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.combinedDockerConfig = Preconditions.CheckNotNull(combinedDockerConfig, nameof(combinedDockerConfig));
        }

        public string Id => $"PullCommand({this.combinedDockerConfig.Image})";

        public async Task ExecuteAsync(CancellationToken token)
        {
            string[] imageParts = this.combinedDockerConfig.Image.Split(':');
            string image;
            string tag;
            if (imageParts.Length > 1)
            {
                image = string.Join(":", imageParts.Take(imageParts.Length - 1));
                tag = imageParts[imageParts.Length - 1];
            }
            else
            {
                image = imageParts[0];
                tag = string.Empty;
            }
            var pullParameters = new ImagesCreateParameters
            {
                FromImage = image,
                Tag = tag
            };

            try
            {
                await this.client.Images.CreateImageAsync(pullParameters,
                                                          this.combinedDockerConfig.AuthConfig.OrDefault(),
                                                          new Progress<JSONMessage>(),
                                                          token);
            }
            catch (DockerApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new ImageNotFoundException(image, tag, ex.StatusCode.ToString(), ex);
                }
                else if (ex.StatusCode == HttpStatusCode.InternalServerError)
                {
                    throw new InternalServerErrorException(image, tag, ex.StatusCode.ToString(), ex);
                }
                //otherwise throw
                throw;
            }
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"docker pull {this.combinedDockerConfig.Image}";
    }
}
