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

    public class RuntimeInfoProviderTest
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(300);
        static readonly IDockerClient Client = DockerHelper.Client;
        static readonly IEntityStore<string, ModuleState> RestartStateStore = new Mock<IEntityStore<string, ModuleState>>().Object;
        static readonly IRestartPolicyManager RestartManager = new Mock<IRestartPolicyManager>().Object;
        const string OperatingSystemType = "linux";
        const string Architecture = "x86_x64";

        [Integration]
        [Fact]
        public async Task TestEmptyEnvironment()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                RuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(Client);
                IEnumerable<ModuleRuntimeInfo> modules = await runtimeInfoProvider.GetModules(cts.Token);
                Assert.Equal(0, modules.Count());
            }
        }

        [Integration]
        [Fact]
        public async Task TestPlatformInfo()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                SystemInfoResponse systemInfo = await Client.System.GetSystemInfoAsync(cts.Token);
                RuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(Client);

                // Act
                SystemInfo recivedSystemInfo = await runtimeInfoProvider.GetSystemInfo();

                // Assert
                Assert.Equal(systemInfo.OSType, recivedSystemInfo.OperatingSystemType);
                Assert.Equal(systemInfo.Architecture, recivedSystemInfo.Architecture);
                Assert.Equal(systemInfo.ServerVersion, recivedSystemInfo.Version);
            }
        }

        [Integration]
        [Fact(Skip = "Flaky test, bug #2494148")]
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
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null, null);

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

                    var credential = new ConnectionStringCredentials("fake");
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(credential);

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
                    RuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(Client);
                    IEnumerable<ModuleRuntimeInfo> modules = await runtimeInfoProvider.GetModules(cts.Token);
                    Assert.Equal(1, modules.Count());
                    Assert.Equal(module.Name, modules.First().Name);
                }
            }
            finally
            {
                await Client.CleanupContainerAsync(Name, Image);
                await Client.CleanupContainerAsync("test-filters-external", Image);
            }
        }

        [Integration]
        [Fact]
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
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null, null);

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

                    var credential = new ConnectionStringCredentials("fake");
                    var identity = new Mock<IModuleIdentity>();
                    identity.Setup(id => id.Credentials).Returns(credential);

                    ICommand create = await CreateCommand.BuildAsync(Client, module, identity.Object, loggingConfig, configSource.Object, false);

                    await Client.PullImageAsync(Image, cts.Token);

                    // create module using command
                    await create.ExecuteAsync(cts.Token);

                    // check that the environment variables are being returned
                    RuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(Client);
                    IEnumerable<ModuleRuntimeInfo> modules = await runtimeInfoProvider.GetModules(cts.Token);
                    var returnedModule = modules.First(m => m.Name == Name) as ModuleRuntimeInfo<DockerReportedConfig>;
                    Assert.NotNull(returnedModule);
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
        public void InspectResponseToModuleTest()
        {
            const string StatusText = "Running for 1 second";
            DateTime lastStartTime = DateTime.Parse("2017-08-04T17:52:13.0419502Z", null, DateTimeStyles.RoundtripKind);
            DateTime lastExitTime = lastStartTime.AddDays(1);
            // Arrange
            string id = Guid.NewGuid().ToString();
            string hash = Guid.NewGuid().ToString();

            var inspectContainerResponse = new ContainerInspectResponse
            {
                State = new ContainerState
                {
                    Status = StatusText,
                    ExitCode = 0,
                    StartedAt = lastStartTime.ToString("o"),
                    FinishedAt = lastExitTime.ToString("o"),
                },
                Name = "/sensor",
                Image = hash
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
            ModuleRuntimeInfo module = RuntimeInfoProvider.InspectResponseToModule(inspectContainerResponse);

            // Assert
            Assert.NotNull(module);
            var dockerModule = module as ModuleRuntimeInfo<DockerReportedConfig>;
            Assert.NotNull(dockerModule);
            Assert.Equal("", dockerModule.Config.Image);

            Assert.Equal("sensor", dockerModule.Name);
            Assert.Equal(0, dockerModule.ExitCode);
            Assert.Equal(StatusText, dockerModule.Description);
            Assert.Equal(lastStartTime, dockerModule.StartTime.OrDefault());
            Assert.Equal(lastExitTime, dockerModule.ExitTime.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetSystemInfoTest()
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
            RuntimeInfoProvider runtimeInfoProvider = await RuntimeInfoProvider.CreateAsync(dockerClient);
            SystemInfo systemInfo = await runtimeInfoProvider.GetSystemInfo();

            // Assert
            Assert.NotNull(systemInfo);
            Assert.Equal(systemInfo.OperatingSystemType, systemInfoResponse.OSType);
            Assert.Equal(systemInfo.Architecture, systemInfoResponse.Architecture);
        }
    }
}
