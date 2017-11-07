// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System;
    using System.IO;
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
            const string Image = "hello-world:latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";
            var tempFile = Path.GetTempFileName();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, cts.Token);
                    var dockerLoggingOptions = new Dictionary<string, string>
                    {
                        { "max-size", "1m" },
                        { "max-file", "1" }
                    };
                    // Logging options will be derived from these default logging options
                    var loggingConfig = new DockerLoggingConfig("json-file", dockerLoggingOptions);
                    var config = new DockerConfig(Image, @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""PortBindings"": {""8080/tcp"": [{""HostPort"": ""80""}]}}}");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString },
                            {Constants.EdgeHostCACertificateFileKey, tempFile },
                            {Constants.EdgeModuleCACertificateFileKey, "/module.ca.cert" }

                        }).Build();

                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", "")), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("fakePrimaryKey"));
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(fakeConnectionString);

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created and has correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    // port mapping
                    Assert.Equal("8080/tcp", container.HostConfig.PortBindings.First().Key);
                    // logging
                    Assert.Equal("json-file", container.HostConfig.LogConfig.Type);
                    Assert.True(container.HostConfig.LogConfig.Config.Count == 2);
                    Assert.Equal("1m", container.HostConfig.LogConfig.Config.GetOrElse("max-size", "missing"));
                    Assert.Equal("1", container.HostConfig.LogConfig.Config.GetOrElse("max-file", "missing"));
                    // environment variables
                    var envMap = container.Config.Env.ToImmutableDictionary(s => s.Split('=', 2)[0], s => s.Split('=', 2)[1]);
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.Equal(fakeConnectionString, envMap["EdgeHubConnectionString"]);
                    // certificates env variables
                    Assert.Equal("/module.ca.cert", envMap[Constants.EdgeModuleCACertificateFileKey]);

                    // mounts
                    Assert.True(container.HostConfig.Mounts.Count == 1);
                    Assert.Equal(container.HostConfig.Mounts[0].Target, "/module.ca.cert");
                    Assert.Equal(container.HostConfig.Mounts[0].Type, "bind");
                    Assert.Equal(container.HostConfig.Mounts[0].ReadOnly, true);
                }
            }
            finally
            {
                File.Delete(tempFile);
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Fact]
        [Integration]
        public async Task TestUdpModuleConfig()
        {
            const string Image = "hello-world:latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, cts.Token);

                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var config = new DockerConfig(Image, @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "EdgeHubConnectionString", fakeConnectionString }
                    }).Build();

                    // Logging options will be derived from application level configuration
                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", @"{""Type"":""json-file"",""Config"":{""max-size"":""100M""}}")), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var credential = "fake";
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(credential);

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created with correct settings.
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    // port bindings
                    Assert.Equal(1, container.HostConfig.PortBindings.Count);
                    Assert.False(container.HostConfig.PortBindings.ContainsKey("8883/tcp"));
                    Assert.False(container.HostConfig.PortBindings.ContainsKey("443/tcp"));
                    // logging
                    Assert.Equal("json-file", container.HostConfig.LogConfig.Type);
                    Assert.True(container.HostConfig.LogConfig.Config.Count == 1);
                    Assert.Equal("100M", container.HostConfig.LogConfig.Config["max-size"]);
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Fact]
        [Integration]
        public async Task EdgeHubLaunch()
        {
            const string Image = "hello-world:latest";
            const string Name = Constants.EdgeHubModuleName;
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";
            var tempFile1 = Path.GetTempFileName();
            var tempFile2 = Path.GetTempFileName();
            var tempFile3 = Path.GetTempFileName();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    var mountMap = new Dictionary<string, string>()
                    {
                        {Constants.EdgeHostCACertificateFileKey, tempFile1},
                        {Constants.EdgeModuleCACertificateFileKey, "/module.ca.cert"},
                        {Constants.EdgeHostHubServerCAChainCertificateFileKey, tempFile2},
                        {Constants.EdgeModuleHubServerCAChainCertificateFileKey, "/module.ca.chain.cert"},
                        {Constants.EdgeHostHubServerCertificateFileKey, tempFile3},
                        {Constants.EdgeModuleHubServerCertificateFileKey, "/module.server.cert"}
                    };
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, cts.Token);
                    var dockerLoggingOptions = new Dictionary<string, string>
                    {
                        { "max-size", "1m" },
                        { "max-file", "1" }
                    };
                    var loggingConfig = new DockerLoggingConfig("json-file", dockerLoggingOptions);
                    // Logging options will be derived from module options.
                    var config = new DockerConfig(Image, @"{""Env"": [""k1=v1"", ""k2=v2""], ""HostConfig"": {""LogConfig"": {""Type"":""none""}, ""PortBindings"": {""8080/tcp"": [{""HostPort"": ""80""}],""443/tcp"": [{""HostPort"": ""11443""}]}}}");
                    var configurationInfo = new ConfigurationInfo();
                    var module = new EdgeHubDockerModule("docker", ModuleStatus.Running, Core.RestartPolicy.Always, config, configurationInfo);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString },
                            {Docker.Constants.NetworkIdKey, "testnetwork" },
                            {Constants.EdgeDeviceHostNameKey, "testdevice" },
                            {Constants.EdgeHostHubServerCAChainCertificateFileKey, mountMap[Constants.EdgeHostHubServerCAChainCertificateFileKey] },
                            {Constants.EdgeModuleHubServerCAChainCertificateFileKey,mountMap[Constants.EdgeModuleHubServerCAChainCertificateFileKey] },
                            {Constants.EdgeHostHubServerCertificateFileKey, mountMap[Constants.EdgeHostHubServerCertificateFileKey] },
                            {Constants.EdgeModuleHubServerCertificateFileKey, mountMap[Constants.EdgeModuleHubServerCertificateFileKey] },
                        }).Build();
                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", "")), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("fakePrimaryKey"));
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(fakeConnectionString);

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, true);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created with correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    // edgeHub doesn't have a version
                    Assert.Equal("missing", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    // port bindings - added default bindings for edgeHub
                    Assert.True(container.HostConfig.PortBindings.ContainsKey("8080/tcp"));
                    Assert.True(container.HostConfig.PortBindings.ContainsKey("8883/tcp"));
                    Assert.Equal("8883", container.HostConfig.PortBindings["8883/tcp"].First().HostPort);
                    Assert.True(container.HostConfig.PortBindings.ContainsKey("443/tcp"));
                    Assert.True(container.HostConfig.PortBindings["443/tcp"].Count == 2);
                    Assert.Equal("11443", container.HostConfig.PortBindings["443/tcp"][0].HostPort);
                    Assert.Equal("443", container.HostConfig.PortBindings["443/tcp"][1].HostPort);
                    // logging
                    Assert.Equal("none", container.HostConfig.LogConfig.Type);
                    Assert.True(container.HostConfig.LogConfig.Config.Count == 0);
                    // network settings
                    Assert.Equal("testdevice", container.NetworkSettings.Networks.GetOrElse("testnetwork", new EndpointSettings()).Aliases.FirstOrDefault());
                    // mounts
                    Assert.True(container.HostConfig.Mounts.Count == 2);
                    Assert.Equal(container.HostConfig.Mounts[0].Target, mountMap[Constants.EdgeModuleHubServerCAChainCertificateFileKey]);
                    Assert.Equal(container.HostConfig.Mounts[0].Type, "bind");
                    Assert.Equal(container.HostConfig.Mounts[0].ReadOnly, true);
                    Assert.Equal(container.HostConfig.Mounts[1].Target, mountMap[Constants.EdgeModuleHubServerCertificateFileKey]);
                    Assert.Equal(container.HostConfig.Mounts[1].Type, "bind");
                    Assert.Equal(container.HostConfig.Mounts[1].ReadOnly, true);

                    Assert.Equal("testdevice", container.NetworkSettings.Networks.GetOrElse("testnetwork", new EndpointSettings()).Aliases.FirstOrDefault());
                    //environment variables
                    var envMap = container.Config.Env.ToImmutableDictionary(s => s.Split('=', 2)[0], s => s.Split('=', 2)[1]);
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.Equal(fakeConnectionString, envMap[Constants.IotHubConnectionStringKey]);
                    // certificates env variables
                    Assert.Equal("/module.ca.chain.cert", envMap[Constants.EdgeModuleHubServerCAChainCertificateFileKey]);
                    Assert.Equal("/module.server.cert", envMap[Constants.EdgeModuleHubServerCertificateFileKey]);
                }
            }
            finally
            {
                File.Delete(tempFile1);
                File.Delete(tempFile2);
                File.Delete(tempFile3);
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Fact]
        [Integration]
        public async Task EdgeHubLaunchWithBadLogOptions()
        {
            const string Image = "hello-world:latest";
            const string Name = Constants.EdgeHubModuleName;
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, cts.Token);
                    var dockerLoggingOptions = new Dictionary<string, string>
                    {
                        { "max-size", "1m" },
                        { "max-file", "1" }
                    };
                    var loggingConfig = new DockerLoggingConfig("json-file", dockerLoggingOptions);
                    var config = new DockerConfig(Image, @"{""Env"": [""k1=v1"", ""k2=v2""]}");
                    var configurationInfo = new ConfigurationInfo("43");
                    var module = new EdgeHubDockerModule("docker", ModuleStatus.Running, Core.RestartPolicy.Always, config, configurationInfo);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString }
                        }).Build();
                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", "Not a valid JSON")), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("fakePrimaryKey"));
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(fakeConnectionString);

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, true);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created with correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1));  // for whatever reason the container name is returned with a starting "/"
                    // labels - edgeHub doesn's have a version
                    Assert.Equal("missing", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    Assert.Equal("43", container.Config.Labels.GetOrElse(Constants.Labels.ConfigurationId, "missing"));
                    // port bindings - check that we added default bindings for hub
                    Assert.True(container.HostConfig.PortBindings.ContainsKey("8883/tcp"));
                    Assert.True(container.HostConfig.PortBindings["8883/tcp"].Count == 1);
                    Assert.Equal("8883", container.HostConfig.PortBindings["8883/tcp"].First().HostPort);
                    Assert.True(container.HostConfig.PortBindings.ContainsKey("443/tcp"));
                    Assert.True(container.HostConfig.PortBindings["443/tcp"].Count == 1);
                    Assert.Equal("443", container.HostConfig.PortBindings["443/tcp"][0].HostPort);
                    // logging
                    Assert.Equal("json-file", container.HostConfig.LogConfig.Type);
                    Assert.True(container.HostConfig.LogConfig.Config.Count == 2);
                    Assert.Equal("1m", container.HostConfig.LogConfig.Config.GetOrElse("max-size", "missing"));
                    Assert.Equal("1", container.HostConfig.LogConfig.Config.GetOrElse("max-file", "missing"));
                    // environment variables
                    var envMap = container.Config.Env.ToImmutableDictionary(s => s.Split('=', 2)[0], s => s.Split('=', 2)[1]);
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.Equal(fakeConnectionString, envMap[Constants.IotHubConnectionStringKey]);
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }
    }
}
