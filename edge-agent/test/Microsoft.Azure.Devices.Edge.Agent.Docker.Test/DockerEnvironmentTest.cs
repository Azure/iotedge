// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
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
            const string FakeConnectionString = "FakeConnectionString";

            try
            {
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    await Client.CleanupContainerAsync(Name, Image);
                    await Client.CleanupContainerAsync("test-filters-external", Image);

                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var config = new DockerConfig(Image, Tag);
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, config);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "EdgeHubConnectionString", FakeConnectionString }
                    }).Build();

                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);

                    var create = new CreateCommand(Client, module, loggingConfig, configSource.Object);

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

        [Fact]
        [Integration]
        public async Task TestEnvVars()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-env";
            const string FakeConnectionString = "FakeConnectionString";

            try
            {
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    await Client.CleanupContainerAsync(Name, Image);

                    var config = new DockerConfig(Image, Tag, new Dictionary<string, string>()
                    {
                        { "k1", "v1" },
                        { "k2", "v2" }
                    });
                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, config);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "EdgeHubConnectionString", FakeConnectionString }
                    }).Build();

                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);

                    var create = new CreateCommand(Client, module, loggingConfig, configSource.Object);

                    await Client.PullImageAsync(Image, Tag, cts.Token);

                    // create module using command
                    await create.ExecuteAsync(cts.Token);

                    // check that the environment variables are being returned
                    var environment = new DockerEnvironment(Client);
                    ModuleSet modules = await environment.GetModulesAsync(cts.Token);
                    Assert.NotNull(modules.Modules[Name]);
                    Assert.Equal("v1", ((DockerModule)modules.Modules[Name]).Config.Env["k1"]);
                    Assert.Equal("v2", ((DockerModule)modules.Modules[Name]).Config.Env["k2"]);
                    Assert.Equal($"{FakeConnectionString};ModuleId={Name}", ((DockerModule)modules.Modules[Name]).Config.Env["EdgeHubConnectionString"]);
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