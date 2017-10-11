// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Xunit;

    [ExcludeFromCodeCoverage]
    [Collection("Docker")]
    public class CreateCommandTest
    {

        [Fact]
        [Integration]
        public async Task SmokeTest()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, Tag, cts.Token);
                    var dockerLoggingOptions = new Dictionary<string, string>
                    {
                        { "max-size", "1m" },
                        { "max-file", "1" }
                    };
                    var loggingConfig = new DockerLoggingConfig("json-file", dockerLoggingOptions);
                    var config = new DockerConfig(Image, Tag, @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""8080/tcp"": [{""HostPort"": ""80""}]}}}");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString }
                        }).Build();

                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);

                    var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("fakePrimaryKey"));
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(fakeConnectionString);

                    var command = new CreateCommand(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    Assert.Equal("8080/tcp", container.HostConfig.PortBindings.First().Key);

                    var envMap = container.Config.Env.ToImmutableDictionary(s => s.Split('=', 2)[0], s => s.Split('=', 2)[1]);
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.Equal(fakeConnectionString, envMap["EdgeHubConnectionString"]);
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Fact]
        [Integration]
        public async Task TestUdpModuleConfig()
        {
            const string Image = "hello-world";
            const string Tag = "latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, Tag, cts.Token);

                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var config = new DockerConfig(Image, Tag, @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "EdgeHubConnectionString", fakeConnectionString }
                    }).Build();

                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);

                    var credential = "fake";
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(credential);

                    var command = new CreateCommand(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    Assert.Equal(1, container.HostConfig.PortBindings.Count);
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }
    }
}
