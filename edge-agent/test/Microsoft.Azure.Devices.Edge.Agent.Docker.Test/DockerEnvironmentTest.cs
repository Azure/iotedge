// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Collection("Docker")]
    public class DockerEnvironmentTest
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        static readonly IDockerClient Client = DockerHelper.Client;

        [Fact]
        [Integration]
        public async Task TestEmptyEnvironment()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                var environment = new DockerEnvironment(Client);
                ModuleSet modules = await environment.GetModulesAsync(cts.Token);
                Assert.Equal(0, modules.Modules.Count);
            }
        }

        [Fact]
        [Integration]
        public async Task TestFilters()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-filters";

            try
            {
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    await Client.CleanupContainerAsync(Name, Image);
                    await Client.CleanupContainerAsync("test-filters-external", Image);

                    var config = new DockerConfig(Image, Tag);
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, config);
                    var create = new CreateCommand(Client, module);

                    // pull the image for both containers
                    await Client.PullImageAsync(Image, Tag, cts.Token);

                    // pull and create module using commands
                    await create.ExecuteAsync(cts.Token);

                    var createParams = new CreateContainerParameters
                    {
                        Name = "test-filters-external",
                        Image = Image + ":" + Tag,
                    };
                    await Client.Containers.CreateContainerAsync(createParams);

                    // Check that only containers created via command are listed in the environment
                    var environment = new DockerEnvironment(Client);
                    ModuleSet modules = await environment.GetModulesAsync(cts.Token);
                    Assert.Equal(1, modules.Modules.Count);
                    Assert.Equal(module.Name, modules.Modules.First().Value.Name);
                }
            }
            finally
            {
                await Client.CleanupContainerAsync(Name, Image);
                await Client.CleanupContainerAsync("test-filters-external", Image);
            }
        }
    }
}