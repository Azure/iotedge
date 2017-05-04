// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class CreateCommandTest
    {
        const int BufferSize = 1 << 19;
        static readonly DockerClient Client = new DockerClientConfiguration(new Uri("http://localhost:2375")).CreateClient();

        [Fact]
        [Bvt]
        public async Task SmokeTest()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-helloworld";

            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                // ensure image has been pulled
                var pullParams = new ImagesPullParameters
                {
                    Parent = Image,
                    Tag = Tag
                };
                Stream stream = await Client.Images.PullImageAsync(pullParams, null);
                await stream.CopyToAsync(Stream.Null, BufferSize, cts.Token);

                var config = new DockerConfig(Image, Tag);
                var module = new DockerModule(Name, "1.0", ModuleStatus.Running, config);
                var command = new CreateCommand(Client, module);

                // run the command
                await command.ExecuteAsync(cts.Token);

                // verify container is created
                ContainerInspectResponse container = await Client.Containers.InspectContainerAsync(Name);
                Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                Assert.Equal("1.0", container.Config.Labels.GetOrElse("version", "missing"));
            }
            finally
            {
                var removeParams = new ContainerRemoveParameters
                {
                    Force = true
                };
                await TestHelper.Safe(() => Client.Containers.RemoveContainerAsync(Name, removeParams));

                var imageParams = new ImageDeleteParameters
                {
                    Force = true,
                    PruneChildren = true
                };
                await TestHelper.Safe(() => Client.Images.DeleteImageAsync(Image, imageParams));
            }
        }
    }
}