// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public static class DockerHelper
    {
        const int BufferSize = 1 << 19;
        static readonly Lazy<IDockerClient> LazyClient = new Lazy<IDockerClient>(GetDockerClient);

        public static IDockerClient Client { get; } = LazyClient.Value;

        static IDockerClient GetDockerClient()
        {
            // Build the docker host URL.
            string dockerHostUrl = ConfigHelper.TestConfig["dockerHostUrl"];
            return new DockerClientConfiguration(new Uri(dockerHostUrl)).CreateClient();
        }

        /// <summary>
        /// Pulls specified image and ensures it is downloaded completely
        /// </summary>
        /// <param name="client"></param>
        /// <param name="image"></param>
        /// <param name="tag"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task PullImageAsync(this IDockerClient client, string image, string tag, CancellationToken token)
        {
            var pullParams = new ImagesCreateParameters
            {
                FromImage = image,
                Tag = tag
            };
            await Client.Images.CreateImageAsync(pullParams, null, new Progress<JSONMessage>(), token);
        }

        /// <summary>
        /// Removes container and deletes image
        /// </summary>
        /// <param name="client"></param>
        /// <param name="name"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static async Task CleanupContainerAsync(this IDockerClient client, string name, string image)
        {
            var removeParams = new ContainerRemoveParameters
            {
                Force = true
            };
            await Safe(() => client.Containers.RemoveContainerAsync(name, removeParams));

            var imageParams = new ImageDeleteParameters
            {
                Force = true,
                PruneChildren = true
            };
            await Safe(() => client.Images.DeleteImageAsync(image, imageParams));
        }

        public static async Task Safe(Func<Task> action)
        {
            try
            {
                await action();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }
    }
}