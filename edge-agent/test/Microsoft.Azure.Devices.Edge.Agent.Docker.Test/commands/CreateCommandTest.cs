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
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
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
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();

        readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(300);

        [Fact]
        [Integration]
        public async Task SmokeTest()
        {
            const string Image = "hello-world:latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(this.defaultTimeout))
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
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.OnUnhealthy, config, ImagePullPolicy.OnCreate, null, EnvVars);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString },
                            { Constants.EdgeModuleCaCertificateFileKey, "/module.ca.cert" }
                        }).Build();

                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", string.Empty)), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(new ConnectionStringCredentials(fakeConnectionString));

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created and has correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1)); // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    // port mapping
                    Assert.Equal("8080/tcp", container.HostConfig.PortBindings.First().Key);
                    // logging
                    Assert.Equal("json-file", container.HostConfig.LogConfig.Type);
                    Assert.True(container.HostConfig.LogConfig.Config.Count == 2);
                    Assert.Equal("1m", container.HostConfig.LogConfig.Config.GetOrElse("max-size", "missing"));
                    Assert.Equal("1", container.HostConfig.LogConfig.Config.GetOrElse("max-file", "missing"));
                    // environment variables
                    IDictionary<string, string> envMap = container.Config.Env.ToDictionary('=');
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.False(envMap.ContainsKey("UpstreamProtocol"));
                    Assert.Equal(fakeConnectionString, envMap["EdgeHubConnectionString"]);
                    // certificates env variables
                    Assert.Equal("/module.ca.cert", envMap[Constants.EdgeModuleCaCertificateFileKey]);
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Integration]
        [Fact]
        public async Task TestUdpModuleConfig()
        {
            const string Image = "hello-world:latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(this.defaultTimeout))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    // ensure image has been pulled
                    await DockerHelper.Client.PullImageAsync(Image, cts.Token);

                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var config = new DockerConfig(Image, @"{""HostConfig"": {""PortBindings"": {""42/udp"": [{""HostPort"": ""42""}]}}}");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.OnUnhealthy, config, ImagePullPolicy.OnCreate, null, EnvVars);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
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

                    string credential = "fake";
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(new ConnectionStringCredentials(credential));

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created with correct settings.
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1)); // for whatever reason the container name is returned with a starting "/"
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

            try
            {
                using (var cts = new CancellationTokenSource(this.defaultTimeout))
                {
                    var mountMap = new Dictionary<string, string>()
                    {
                        { Constants.EdgeModuleCaCertificateFileKey, "/module.ca.cert" },
                        { Constants.EdgeModuleHubServerCaChainCertificateFileKey, "/module.ca.chain.cert" },
                        { Constants.EdgeModuleHubServerCertificateFileKey, "/module.server.cert" }
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
                    var module = new EdgeHubDockerModule("docker", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, config, ImagePullPolicy.Never, configurationInfo, EnvVars);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString },
                            { Constants.NetworkIdKey, "testnetwork" },
                            { Constants.EdgeDeviceHostNameKey, "testdevice" },
                            { Constants.EdgeModuleHubServerCaChainCertificateFileKey, mountMap[Constants.EdgeModuleHubServerCaChainCertificateFileKey] },
                            { Constants.EdgeModuleHubServerCertificateFileKey, mountMap[Constants.EdgeModuleHubServerCertificateFileKey] },
                        }).Build();
                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", string.Empty)), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(new ConnectionStringCredentials(fakeConnectionString));

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, true);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created with correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1)); // for whatever reason the container name is returned with a starting "/"
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

                    Assert.Equal("testdevice", container.NetworkSettings.Networks.GetOrElse("testnetwork", new EndpointSettings()).Aliases.FirstOrDefault());
                    // environment variables
                    IDictionary<string, string> envMap = container.Config.Env.ToDictionary('=');
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.Equal(fakeConnectionString, envMap[Constants.IotHubConnectionStringKey]);
                    // certificates env variables
                    Assert.Equal("/module.ca.chain.cert", envMap[Constants.EdgeModuleHubServerCaChainCertificateFileKey]);
                    Assert.Equal("/module.server.cert", envMap[Constants.EdgeModuleHubServerCertificateFileKey]);
                }
            }
            finally
            {
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
                using (var cts = new CancellationTokenSource(this.defaultTimeout))
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
                    var module = new EdgeHubDockerModule("docker", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always, config, ImagePullPolicy.OnCreate, configurationInfo, EnvVars);

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

                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(new ConnectionStringCredentials(fakeConnectionString));

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, true);

                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // verify container is created with correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1)); // for whatever reason the container name is returned with a starting "/"
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
                    IDictionary<string, string> envMap = container.Config.Env.ToDictionary('=');
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

        [Fact]
        [Unit]
        public async Task TestMountEdgeHubVolume()
        {
            // Arrange
            const string VolumeName = "vol1";
            const string VolumePath = "/azure-edge/vol1";

            CreateContainerParameters createContainerParameters = null;
            var containerOperations = new Mock<IContainerOperations>();
            containerOperations.Setup(co => co.CreateContainerAsync(It.IsAny<CreateContainerParameters>(), It.IsAny<CancellationToken>()))
                .Callback((CreateContainerParameters ccp, CancellationToken tok) => createContainerParameters = ccp)
                .ReturnsAsync(new CreateContainerResponse());

            var dockerClient = new Mock<IDockerClient>();
            dockerClient.SetupGet(c => c.Containers)
                .Returns(containerOperations.Object);

            var moduleIdentity = new Mock<IModuleIdentity>();
            moduleIdentity.SetupGet(i => i.Credentials)
                .Returns(new ConnectionStringCredentials(string.Empty));

            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(r => r.Config)
                .Returns(new DockerRuntimeConfig("1.25", string.Empty));

            var systemModules = new SystemModules(
                this.CreateMockEdgeAgentModule(),
                this.CreateMockEdgeHubModule());

            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeHubVolumeNameKey, VolumeName },
                    { Constants.EdgeHubVolumePathKey, VolumePath }
                }).Build();

            var configSource = new Mock<IConfigSource>();
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo.Object,
                systemModules,
                ImmutableDictionary<string, IModule>.Empty);
            var deploymentConfigInfo = new DeploymentConfigInfo(10, deploymentConfig);
            configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            configSource.SetupGet(cs => cs.Configuration)
                .Returns(configuration);

            // Act
            ICommand createCommand = await CreateCommand.BuildAsync(
                dockerClient.Object,
                new DockerModule(
                    "mod1",
                    "1.0",
                    ModuleStatus.Running,
                    global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.OnUnhealthy,
                    new DockerConfig("image1"),
                    ImagePullPolicy.OnCreate,
                    new ConfigurationInfo("1234"),
                    EnvVars),
                moduleIdentity.Object,
                new DockerLoggingConfig("json"),
                configSource.Object,
                true);
            await createCommand.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.Contains($"{VolumeName}:{VolumePath}:ro", createContainerParameters.HostConfig.Binds);
        }

        [Fact]
        [Unit]
        public async Task TestMountModuleVolume()
        {
            // Arrange
            const string VolumeName = "vol1";
            const string VolumePath = "/azure-edge/vol1";

            CreateContainerParameters createContainerParameters = null;
            var containerOperations = new Mock<IContainerOperations>();
            containerOperations.Setup(co => co.CreateContainerAsync(It.IsAny<CreateContainerParameters>(), It.IsAny<CancellationToken>()))
                .Callback((CreateContainerParameters ccp, CancellationToken tok) => createContainerParameters = ccp)
                .ReturnsAsync(new CreateContainerResponse());

            var dockerClient = new Mock<IDockerClient>();
            dockerClient.SetupGet(c => c.Containers)
                .Returns(containerOperations.Object);

            var moduleIdentity = new Mock<IModuleIdentity>();
            moduleIdentity.SetupGet(i => i.Credentials)
                .Returns(new ConnectionStringCredentials(string.Empty));

            var runtimeInfo = new Mock<IRuntimeInfo<DockerRuntimeConfig>>();
            runtimeInfo.SetupGet(r => r.Config)
                .Returns(new DockerRuntimeConfig("1.25", string.Empty));

            var systemModules = new SystemModules(
                this.CreateMockEdgeAgentModule(),
                this.CreateMockEdgeHubModule());

            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { Constants.EdgeModuleVolumeNameKey, VolumeName },
                    { Constants.EdgeModuleVolumePathKey, VolumePath }
                }).Build();

            var configSource = new Mock<IConfigSource>();
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo.Object,
                systemModules,
                ImmutableDictionary<string, IModule>.Empty);
            var deploymentConfigInfo = new DeploymentConfigInfo(10, deploymentConfig);
            configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync())
                .ReturnsAsync(deploymentConfigInfo);
            configSource.SetupGet(cs => cs.Configuration)
                .Returns(configuration);

            // Act
            ICommand createCommand = await CreateCommand.BuildAsync(
                dockerClient.Object,
                new DockerModule(
                    "mod1",
                    "1.0",
                    ModuleStatus.Running,
                    global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.OnUnhealthy,
                    new DockerConfig("image1"),
                    ImagePullPolicy.OnCreate,
                    new ConfigurationInfo("1234"),
                    EnvVars),
                moduleIdentity.Object,
                new DockerLoggingConfig("json"),
                configSource.Object,
                false);
            await createCommand.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.Contains($"{VolumeName}:{VolumePath}:ro", createContainerParameters.HostConfig.Binds);
        }

        [Fact]
        [Unit]
        public void InjectPortBindingsTest()
        {
            // Make sure the call is a no-op if not injecting for EdgeHub
            // Arrange
            var createContainerParameters = new CreateContainerParameters();
            // Act
            CreateCommand.InjectPortBindings(createContainerParameters, false);
            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.Null(createContainerParameters.HostConfig);

            // Make sure EdgeHub default port mappings is injected
            // Arrange
            createContainerParameters = new CreateContainerParameters();
            // Act
            CreateCommand.InjectPortBindings(createContainerParameters, true);
            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.NotNull(createContainerParameters.HostConfig);
            Assert.NotNull(createContainerParameters.HostConfig.PortBindings);
            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("8883/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["8883/tcp"].Count);
            Assert.Equal("8883", createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostIP);

            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("443/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["443/tcp"].Count);
            Assert.Equal("443", createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostIP);

            // If EdgeHub port mappings are already in the create options, then they are not injected twice
            // Arrange
            createContainerParameters = new CreateContainerParameters
            {
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "8883/tcp", new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostPort = "8883"
                                }
                            }
                        },
                        {
                            "443/tcp", new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostPort = "443"
                                }
                            }
                        }
                    }
                }
            };
            // Act
            CreateCommand.InjectPortBindings(createContainerParameters, true);
            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.NotNull(createContainerParameters.HostConfig);
            Assert.NotNull(createContainerParameters.HostConfig.PortBindings);
            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("8883/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["8883/tcp"].Count);
            Assert.Equal("8883", createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostIP);

            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("443/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["443/tcp"].Count);
            Assert.Equal("443", createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostIP);

            // If EdgeHub port mappings are already in the create options, then they are not injected twice
            // Arrange
            createContainerParameters = new CreateContainerParameters
            {
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "8883/tcp", new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostPort = "8883",
                                    HostIP = "1.2.3.4"
                                }
                            }
                        },
                        {
                            "443/tcp", new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostPort = "443"
                                }
                            }
                        }
                    }
                }
            };
            // Act
            CreateCommand.InjectPortBindings(createContainerParameters, true);
            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.NotNull(createContainerParameters.HostConfig);
            Assert.NotNull(createContainerParameters.HostConfig.PortBindings);
            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("8883/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["8883/tcp"].Count);
            Assert.Equal("8883", createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostPort);
            Assert.Equal("1.2.3.4", createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostIP);

            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("443/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["443/tcp"].Count);
            Assert.Equal("443", createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostIP);

            // If EdgeHub port mappings are already in the create options, then they are not injected twice
            // Arrange
            createContainerParameters = new CreateContainerParameters
            {
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "1234/tcp", new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostPort = "1234"
                                }
                            }
                        }
                    }
                }
            };
            // Act
            CreateCommand.InjectPortBindings(createContainerParameters, true);
            // Assert
            Assert.NotNull(createContainerParameters);
            Assert.NotNull(createContainerParameters.HostConfig);
            Assert.NotNull(createContainerParameters.HostConfig.PortBindings);
            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("8883/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["8883/tcp"].Count);
            Assert.Equal("8883", createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["8883/tcp"].First().HostIP);
            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("443/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["443/tcp"].Count);
            Assert.Equal("443", createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["443/tcp"].First().HostIP);
            Assert.True(createContainerParameters.HostConfig.PortBindings.ContainsKey("1234/tcp"));
            Assert.Equal(1, createContainerParameters.HostConfig.PortBindings["1234/tcp"].Count);
            Assert.Equal("1234", createContainerParameters.HostConfig.PortBindings["1234/tcp"].First().HostPort);
            Assert.Null(createContainerParameters.HostConfig.PortBindings["1234/tcp"].First().HostIP);
        }

        [Fact]
        [Integration]
        public async Task UpstreamProtocolTest()
        {
            const string Image = "hello-world:latest";
            const string Name = "test-helloworld";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(this.defaultTimeout))
                {
                    // Arrange
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
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.OnUnhealthy, config, ImagePullPolicy.OnCreate, null, EnvVars);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "EdgeHubConnectionString", fakeConnectionString },
                            { Constants.EdgeModuleCaCertificateFileKey, "/module.ca.cert" },
                            { "UpstreamProtocol", "AmqpWs" }
                        }).Build();

                    var modules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", string.Empty)), systemModules, modules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(new ConnectionStringCredentials(fakeConnectionString));

                    ICommand command = await CreateCommand.BuildAsync(DockerHelper.Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    // Act
                    // run the command
                    await command.ExecuteAsync(cts.Token);

                    // Assert
                    // verify container is created and has correct settings
                    ContainerInspectResponse container = await DockerHelper.Client.Containers.InspectContainerAsync(Name);
                    Assert.Equal(Name, container.Name.Substring(1)); // for whatever reason the container name is returned with a starting "/"
                    Assert.Equal("1.0", container.Config.Labels.GetOrElse(Constants.Labels.Version, "missing"));
                    // port mapping
                    Assert.Equal("8080/tcp", container.HostConfig.PortBindings.First().Key);
                    // logging
                    Assert.Equal("json-file", container.HostConfig.LogConfig.Type);
                    Assert.True(container.HostConfig.LogConfig.Config.Count == 2);
                    Assert.Equal("1m", container.HostConfig.LogConfig.Config.GetOrElse("max-size", "missing"));
                    Assert.Equal("1", container.HostConfig.LogConfig.Config.GetOrElse("max-file", "missing"));
                    // environment variables
                    IDictionary<string, string> envMap = container.Config.Env.ToDictionary('=');
                    Assert.Equal("v1", envMap["k1"]);
                    Assert.Equal("v2", envMap["k2"]);
                    Assert.Equal("AmqpWs", envMap["UpstreamProtocol"]);
                    Assert.Equal(fakeConnectionString, envMap["EdgeHubConnectionString"]);
                    // certificates env variables
                    Assert.Equal("/module.ca.cert", envMap[Constants.EdgeModuleCaCertificateFileKey]);
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        IEdgeAgentModule CreateMockEdgeAgentModule() => new TestAgentModule(
            Constants.EdgeAgentModuleName,
            "docker",
            new TestConfig("EdgeAgentImage"),
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            EnvVars);

        IEdgeHubModule CreateMockEdgeHubModule() => new TestHubModule(
            Constants.EdgeHubModuleName,
            "docker",
            ModuleStatus.Running,
            new TestConfig("EdgeAgentImage"),
            global::Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            EnvVars);
    }
}
