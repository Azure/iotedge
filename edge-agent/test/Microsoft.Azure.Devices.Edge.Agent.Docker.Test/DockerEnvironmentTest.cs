// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Xunit;

    [Collection("Docker")]
    public class DockerEnvironmentTest
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        static readonly IDockerClient Client = DockerHelper.Client;
        static readonly IEntityStore<string, ModuleState> RestartStateStore = new Mock<IEntityStore<string, ModuleState>>().Object;
        static readonly IRestartPolicyManager RestartManager = new Mock<IRestartPolicyManager>().Object;
        const string OperatingSystemType = "linux";
        const string Architecture = "x86_x64";

        //TODO: INVESTIGATE/FIX AND ENABLE ASAP. Bug: 1912576
        [Fact(Skip = "Failing, needs investigation.")]
        [Integration]
        public async Task TestEmptyEnvironment()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                DockerEnvironment environment = await DockerEnvironment.CreateAsync(Client, RestartStateStore, RestartManager);
                ModuleSet modules = await environment.GetModulesAsync(cts.Token);
                Assert.Equal(0, modules.Modules.Count);
            }
        }

        //TODO: INVESTIGATE/FIX AND ENABLE ASAP. Bug: 1912576
        [Fact(Skip = "Failing, needs investigation.")]
        [Integration]
        public async Task TestPlatformInfo()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                SystemInfoResponse systemInfo = await Client.System.GetSystemInfoAsync(cts.Token);

                // Act
                DockerEnvironment environment = await DockerEnvironment.CreateAsync(Client, RestartStateStore, RestartManager);

                // Assert
                Assert.Equal(systemInfo.OSType, environment.OperatingSystemType);
                Assert.Equal(systemInfo.Architecture, environment.Architecture);
            }
        }

        //TODO: INVESTIGATE/FIX AND ENABLE ASAP. Bug: 1912576
        [Fact(Skip = "Failing, needs investigation.")]
        [Integration]
        public async Task TestFilters()
        {
            const string Image = "hello-world:latest";
            const string Name = "test-filters";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    await Client.CleanupContainerAsync(Name, Image);
                    await Client.CleanupContainerAsync("test-filters-external", Image);

                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var config = new DockerConfig(Image);
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "EdgeDeviceConnectionString", fakeConnectionString }
                    }).Build();

                    var deploymentConfigModules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", "")), systemModules, deploymentConfigModules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    string credential = "fake";
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(credential);

                    ICommand create = await CreateCommand.BuildAsync(Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    // pull the image for both containers
                    await Client.PullImageAsync(Image, cts.Token);

                    // pull and create module using commands
                    await create.ExecuteAsync(cts.Token);

                    var createParams = new CreateContainerParameters
                    {
                        Name = "test-filters-external",
                        Image = Image,
                    };
                    await Client.Containers.CreateContainerAsync(createParams);

                    // Check that only containers created via command are listed in the environment
                    DockerEnvironment environment = await DockerEnvironment.CreateAsync(Client, RestartStateStore, RestartManager);
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

        //TODO: INVESTIGATE/FIX AND ENABLE ASAP. Bug: 1912576
        [Fact(Skip = "Failing, needs investigation.")]
        [Integration]
        public async Task TestEnvVars()
        {
            const string Image = "hello-world:latest";
            const string Name = "test-env";
            string sharedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("deviceKey"));
            string fakeConnectionString = $"Hostname=fakeiothub;Deviceid=test;SharedAccessKey={sharedAccessKey}";

            try
            {
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    await Client.CleanupContainerAsync(Name, Image);

                    string createOptions = @"{""Env"": [ ""k1=v1"", ""k2=v2""]}";
                    var config = new DockerConfig(Image, createOptions);
                    var loggingConfig = new DockerLoggingConfig("json-file");
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null);

                    IConfigurationRoot configRoot = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "EdgeDeviceConnectionString", fakeConnectionString }
                    }).Build();

                    var deploymentConfigModules = new Dictionary<string, IModule> { [Name] = module };
                    var systemModules = new SystemModules(null, null);
                    var deploymentConfigInfo = new DeploymentConfigInfo(1, new DeploymentConfig("1.0", new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.25", "")), systemModules, deploymentConfigModules));
                    var configSource = new Mock<IConfigSource>();
                    configSource.Setup(cs => cs.Configuration).Returns(configRoot);
                    configSource.Setup(cs => cs.GetDeploymentConfigInfoAsync()).ReturnsAsync(deploymentConfigInfo);

                    string credential = "fake";
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.ConnectionString).Returns(credential);

                    ICommand create = await CreateCommand.BuildAsync(Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    await Client.PullImageAsync(Image, cts.Token);

                    // create module using command
                    await create.ExecuteAsync(cts.Token);

                    // check that the environment variables are being returned
                    DockerEnvironment environment = await DockerEnvironment.CreateAsync(Client, RestartStateStore, RestartManager);
                    ModuleSet modules = await environment.GetModulesAsync(cts.Token);
                    Assert.NotNull(modules.Modules[Name]);
                    Assert.True(((DockerRuntimeModule)modules.Modules[Name]).Config.CreateOptions.Env.Contains("k1=v1"));
                    Assert.True(((DockerRuntimeModule)modules.Modules[Name]).Config.CreateOptions.Env.Contains("k2=v2"));
                }
            }
            finally
            {
                await Client.CleanupContainerAsync(Name, Image);
                await Client.CleanupContainerAsync("test-filters-external", Image);
            }
        }

        [Fact]
        [Unit]
        public async Task ContainerToModuleTest()
        {
            const string StatusText = "Running for 1 second";
            DateTime lastStartTime = DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind);
            DateTime lastExitTime = lastStartTime.AddDays(1);
            // Arrange
            string id = Guid.NewGuid().ToString();
            var containerListResponse = new ContainerListResponse
            {
                Image = "localhost:5000/sensor:v2",
                Names = new List<string> { "/sensor" },
                ID = id,
                State = "running",
                Labels = new Dictionary<string, string> { { Constants.Labels.Version, "v2" } }
            };
            var inspectContainerResponse = new ContainerInspectResponse
            {
                State = new ContainerState
                {
                    Status = StatusText,
                    ExitCode = 0,
                    StartedAt = lastStartTime.ToString("o"),
                    FinishedAt = lastExitTime.ToString("o")
                }
            };

            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };

            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.Containers == Mock.Of<IContainerOperations>(co => co.InspectContainerAsync(id, default(CancellationToken)) == Task.FromResult(inspectContainerResponse)) &&
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));

            // Act
            DockerEnvironment dockerEnvironment = await DockerEnvironment.CreateAsync(dockerClient, RestartStateStore, RestartManager);
            IModule module = await dockerEnvironment.ContainerToModuleAsync(containerListResponse);

            // Assert
            Assert.NotNull(module);
            var dockerModule = module as DockerRuntimeModule;
            Assert.NotNull(dockerModule);
            Assert.Equal("localhost:5000/sensor:v2", dockerModule.Config.Image);
            Assert.Equal(0, dockerModule.Config.CreateOptions.Env?.Count ?? 0);
            Assert.Equal(0, dockerModule.Config.CreateOptions.HostConfig?.PortBindings?.Count ?? 0);

            Assert.Equal("sensor", dockerModule.Name);
            Assert.Equal("v2", dockerModule.Version);
            Assert.Equal(ModuleStatus.Running, dockerModule.DesiredStatus);
            Assert.Equal(0, dockerModule.ExitCode);
            Assert.Equal(StatusText, dockerModule.StatusDescription);
            Assert.Equal(lastStartTime, dockerModule.LastStartTimeUtc);
            Assert.Equal(lastExitTime, dockerModule.LastExitTimeUtc);
            Assert.Equal(OperatingSystemType, dockerEnvironment.OperatingSystemType);
            Assert.Equal(Architecture, dockerEnvironment.Architecture);
        }

        [Fact]
        [Unit]
        public async Task GetUpdatedRuntimeInfoAsyncTest()
        {

            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };

            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));

            var inputRuntimeInfo = new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.13", string.Empty));

            // Act
            DockerEnvironment dockerEnvironment = await DockerEnvironment.CreateAsync(dockerClient, RestartStateStore, RestartManager);
            IRuntimeInfo outputRuntimeInfo = await dockerEnvironment.GetUpdatedRuntimeInfoAsync(inputRuntimeInfo);

            // Assert
            Assert.NotNull(outputRuntimeInfo);
            var dockerReportedRuntimeInfo = outputRuntimeInfo as DockerReportedRuntimeInfo;
            Assert.NotNull(dockerReportedRuntimeInfo);

            var expectedRuntimeInfo = new DockerReportedRuntimeInfo("docker", inputRuntimeInfo.Config, new DockerPlatformInfo(OperatingSystemType, Architecture));
            Assert.Equal(expectedRuntimeInfo, dockerReportedRuntimeInfo);
        }

        [Fact]
        [Unit]
        public async Task DockerEnvironmentCreateNullParameterCheck()
        {

            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await DockerEnvironment.CreateAsync(null, store, restartPolicyManager));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await DockerEnvironment.CreateAsync(dockerClient, null, restartPolicyManager));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await DockerEnvironment.CreateAsync(dockerClient, store, null));
        }

        [Fact]
        [Unit]
        public async Task GetRuntimeInfoTest()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            var dockerRuntime = Mock.Of<IRuntimeInfo<DockerRuntimeConfig>>(dr =>
                dr.Type == "docker" &&
                dr.Config == new Mock<DockerRuntimeConfig>("1.25", "").Object);

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            // act
            IRuntimeInfo reportedRuntimeInfo = await environment.GetUpdatedRuntimeInfoAsync(dockerRuntime);

            // assert
            Assert.True(reportedRuntimeInfo is DockerReportedRuntimeInfo);
            var dockerReported = reportedRuntimeInfo as DockerReportedRuntimeInfo;
            Assert.Equal(OperatingSystemType, dockerReported.Platform.OperatingSystemType);
            Assert.Equal(Architecture, dockerReported.Platform.Architecture);
            Assert.NotNull(dockerReported.Config);
        }

        [Fact]
        [Unit]
        public async Task GetUnknownRuntimeInfoTest()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            // act
            IRuntimeInfo reportedRuntimeInfo = await environment.GetUpdatedRuntimeInfoAsync(UnknownRuntimeInfo.Instance);

            // assert
            Assert.True(reportedRuntimeInfo is DockerReportedUnknownRuntimeInfo);
            var dockerReported = reportedRuntimeInfo as DockerReportedUnknownRuntimeInfo;
            Assert.Equal(OperatingSystemType, dockerReported.Platform.OperatingSystemType);
            Assert.Equal(Architecture, dockerReported.Platform.Architecture);
        }

        [Fact]
        [Unit]
        public async Task GetNullRuntimeInfoTest()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            // act
            IRuntimeInfo reportedRuntimeInfo = await environment.GetUpdatedRuntimeInfoAsync(null);

            // assert
            Assert.True(reportedRuntimeInfo is DockerReportedUnknownRuntimeInfo);
            var dockerReported = reportedRuntimeInfo as DockerReportedUnknownRuntimeInfo;
            Assert.Equal(OperatingSystemType, dockerReported.Platform.OperatingSystemType);
            Assert.Equal(Architecture, dockerReported.Platform.Architecture);
        }

        [Fact]
        [Unit]
        public async Task BadRuntimeInfoTest()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            var dockerRuntime = Mock.Of<IRuntimeInfo<DockerRuntimeConfig>>(dr =>
                dr.Type == "not docker" &&
                dr.Config == null);

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            // act, assert
            Assert.Same(dockerRuntime, await environment.GetUpdatedRuntimeInfoAsync(dockerRuntime));
        }

        [Fact]
        [Unit]
        public async Task NullRuntimeInfoTest()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            var dockerRuntime = Mock.Of<IRuntimeInfo<string>>(dr =>
                dr.Type == "docker" &&
                dr.Config == null);

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            // act
            IRuntimeInfo reportedRuntimeInfo = await environment.GetUpdatedRuntimeInfoAsync(dockerRuntime);

            //. assert
            Assert.True(reportedRuntimeInfo is DockerReportedRuntimeInfo);
            var dockerReported = reportedRuntimeInfo as DockerReportedRuntimeInfo;
            Assert.Equal(OperatingSystemType, dockerReported.Platform.OperatingSystemType);
            Assert.Equal(Architecture, dockerReported.Platform.Architecture);
            Assert.True(string.IsNullOrEmpty(dockerReported.Config.MinDockerVersion));
            Assert.True(string.IsNullOrEmpty(dockerReported.Config.LoggingOptions));
        }

        [Fact]
        [Unit]
        async Task TestGetEdgeAgentModuleAsyncContainerNotFound()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var containersMock = new Mock<IContainerOperations>();
            containersMock.Setup(co => co.InspectContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.ExpectationFailed, "failed"));
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)) &&
                dc.Containers == containersMock.Object);
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            var edgeAgent = await environment.GetEdgeAgentModuleAsync(CancellationToken.None) as EdgeAgentDockerRuntimeModule;
            Assert.NotNull(edgeAgent);
            Assert.Equal(edgeAgent.Type, "docker");
            Assert.Equal(DockerReportedConfig.Unknown, edgeAgent.Config);
        }

        [Fact]
        [Unit]
        async Task TestGetEdgeAgentModuleAsync()
        {
            // Arrange
            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            var dockerClient = Mock.Of<IDockerClient>(dc =>
                dc.System == Mock.Of<ISystemOperations>(so => so.GetSystemInfoAsync(default(CancellationToken)) == Task.FromResult(systemInfoResponse)) &&
                dc.Containers == Mock.Of<IContainerOperations>(co =>
                  co.InspectContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(Mock.Of<ContainerInspectResponse>(cir =>
                     cir.Config == new Config() { Image = "myImage" }
                     &&
                     cir.Image == "sha256:foo"
                     &&
                     cir.State == new ContainerState()
                     {
                         StartedAt = "2017-11-13T23:44:35.127381Z"
                     }))));
            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            DockerEnvironment environment = await DockerEnvironment.CreateAsync(dockerClient, store, restartPolicyManager);

            var edgeAgent = await environment.GetEdgeAgentModuleAsync(CancellationToken.None) as EdgeAgentDockerRuntimeModule;
            Assert.NotNull(edgeAgent);
            Assert.Equal(edgeAgent.Type, "docker");
            Assert.Equal("myImage", edgeAgent.Config.Image);
            Assert.Equal("sha256:foo", (edgeAgent.Config as DockerReportedConfig)?.ImageHash);
            Assert.Equal(
                DateTime.Parse("2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind),
                edgeAgent.LastStartTimeUtc
            );
        }

        [Fact]
        [Unit]
        public async void TestModuleListingForEdgeHubModule()
        {
            // Arrange
            var dockerClient = new Mock<IDockerClient>();
            var entityStore = new Mock<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = new Mock<IRestartPolicyManager>();
            var systemOperations = new Mock<ISystemOperations>();
            var containerOperations = new Mock<IContainerOperations>();

            var systemInfoResponse = new SystemInfoResponse
            {
                OSType = OperatingSystemType,
                Architecture = Architecture
            };
            dockerClient.SetupGet(c => c.System).Returns(systemOperations.Object);
            systemOperations.Setup(s => s.GetSystemInfoAsync(CancellationToken.None)).ReturnsAsync(systemInfoResponse);

            var containerListResponse = new ContainerListResponse
            {
                Image = "edge.azrecr.io/edge-hub:1",
                Names = new List<string> { "/edgeHub" },
                ID = Guid.NewGuid().ToString(),
                State = "running",
                Labels = new Dictionary<string, string>
                {
                    { Constants.Labels.Version, "v2" }
                }
            };
            var inspectContainerResponse = new ContainerInspectResponse
            {
                State = new ContainerState
                {
                    Status = "Running for 10 hours",
                    ExitCode = 0,
                    StartedAt = (DateTime.UtcNow - TimeSpan.FromHours(10)).ToString("o"),
                    FinishedAt = DateTime.MinValue.ToString("o")
                }
            };

            dockerClient.SetupGet(c => c.Containers).Returns(containerOperations.Object);
            containerOperations.Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), CancellationToken.None))
                .ReturnsAsync(new[] { containerListResponse });
            containerOperations.Setup(c => c.InspectContainerAsync(containerListResponse.ID, CancellationToken.None))
                .ReturnsAsync(inspectContainerResponse);

            // Act
            DockerEnvironment dockerEnvironment = await DockerEnvironment.CreateAsync(dockerClient.Object, entityStore.Object, restartPolicyManager.Object);
            ModuleSet modules = await dockerEnvironment.GetModulesAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(modules);
            Assert.NotNull(modules.Modules);
            Assert.Equal(1, modules.Modules.Count);
            Assert.True(modules.Modules[Constants.EdgeHubModuleName] is IEdgeHubModule);
        }
    }
}
