// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Collection("Docker")]
    public class DockerEnvironmentTest
    {
        const string OperatingSystemType = "linux";
        const string Architecture = "x86_x64";
        const string Version = "17.11.0-ce";

        [Fact]
        [Unit]
        public async Task GetRuntimeInfoTest()
        {
            // Arrange
            var systemInfo = new SystemInfo(OperatingSystemType, Architecture, Version);

            var store = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            var runtimeInfoProvider = Mock.Of<IRuntimeInfoProvider>(r => r.GetSystemInfo() == Task.FromResult(systemInfo));
            var moduleStateStore = Mock.Of<IEntityStore<string, ModuleState>>();
            string minDockerVersion = "20";
            string dockerLoggingOptions = "dummy logging options";

            var deploymentConfig = new DeploymentConfig(
                "1.0",
                new DockerRuntimeInfo("docker", new DockerRuntimeConfig(minDockerVersion, dockerLoggingOptions)),
                new SystemModules(Option.None<IEdgeAgentModule>(), Option.None<IEdgeHubModule>()),
                new Dictionary<string, IModule>());

            var environment = new DockerEnvironment(runtimeInfoProvider, deploymentConfig, moduleStateStore, restartPolicyManager, systemInfo.OperatingSystemType, systemInfo.Architecture, systemInfo.Version);

            // act
            IRuntimeInfo reportedRuntimeInfo = await environment.GetRuntimeInfoAsync();

            // assert
            Assert.True(reportedRuntimeInfo is DockerReportedRuntimeInfo);
            var dockerReported = reportedRuntimeInfo as DockerReportedRuntimeInfo;
            Assert.Equal(OperatingSystemType, dockerReported.Platform.OperatingSystemType);
            Assert.Equal(Architecture, dockerReported.Platform.Architecture);
            Assert.Equal(Version, dockerReported.Platform.Version);
            Assert.Equal(minDockerVersion, dockerReported.Config.MinDockerVersion);
            Assert.Equal(dockerLoggingOptions, dockerReported.Config.LoggingOptions);
        }

        [Fact]
        [Unit]
        public async Task GetUnknownRuntimeInfoTest()
        {
            // Arrange
            var runtimeInfoProvider = Mock.Of<IRuntimeInfoProvider>();
            var moduleStateStore = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            var environment = new DockerEnvironment(runtimeInfoProvider, DeploymentConfig.Empty, moduleStateStore, restartPolicyManager, OperatingSystemType, Architecture, Version);

            // act
            IRuntimeInfo reportedRuntimeInfo = await environment.GetRuntimeInfoAsync();

            // assert
            Assert.True(reportedRuntimeInfo is DockerReportedUnknownRuntimeInfo);
            var dockerReported = reportedRuntimeInfo as DockerReportedUnknownRuntimeInfo;
            Assert.Equal(OperatingSystemType, dockerReported.Platform.OperatingSystemType);
            Assert.Equal(Architecture, dockerReported.Platform.Architecture);
            Assert.Equal(Version, dockerReported.Platform.Version);
        }

        [Fact]
        [Unit]
        public async Task GetModulesTest()
        {
            // Arrange
            var restartPolicyManager = new Mock<IRestartPolicyManager>();
            restartPolicyManager.Setup(
                    r => r.ComputeModuleStatusFromRestartPolicy(
                        It.IsAny<ModuleStatus>(),
                        It.IsAny<RestartPolicy>(),
                        It.IsAny<int>(),
                        It.IsAny<DateTime>()))
                .Returns<ModuleStatus, RestartPolicy, int, DateTime>((m, r, c, d) => m);

            string module1Hash = Guid.NewGuid().ToString();
            string module2Hash = Guid.NewGuid().ToString();
            string edgeHubHash = Guid.NewGuid().ToString();
            string edgeAgentHash = Guid.NewGuid().ToString();
            var moduleRuntimeInfoList = new List<ModuleRuntimeInfo>();
            moduleRuntimeInfoList.Add(
                new ModuleRuntimeInfo<DockerReportedConfig>(
                    "module1",
                    "docker",
                    ModuleStatus.Stopped,
                    "dummy1",
                    0,
                    Option.Some(new DateTime(2017, 10, 10)),
                    Option.None<DateTime>(),
                    new DockerReportedConfig("mod1:v1", string.Empty, module1Hash)));
            moduleRuntimeInfoList.Add(
                new ModuleRuntimeInfo<DockerReportedConfig>(
                    "module2",
                    "docker",
                    ModuleStatus.Failed,
                    "dummy2",
                    5,
                    Option.Some(new DateTime(2017, 10, 12)),
                    Option.Some(new DateTime(2017, 10, 14)),
                    new DockerReportedConfig("mod2:v2", string.Empty, module2Hash)));
            moduleRuntimeInfoList.Add(
                new ModuleRuntimeInfo<DockerReportedConfig>(
                    "edgeHub",
                    "docker",
                    ModuleStatus.Running,
                    string.Empty,
                    0,
                    Option.Some(new DateTime(2017, 10, 10)),
                    Option.None<DateTime>(),
                    new DockerReportedConfig("edgehub:v1", string.Empty, edgeHubHash)));
            moduleRuntimeInfoList.Add(
                new ModuleRuntimeInfo<DockerReportedConfig>(
                    "edgeAgent",
                    "docker",
                    ModuleStatus.Running,
                    string.Empty,
                    0,
                    Option.Some(new DateTime(2017, 10, 10)),
                    Option.None<DateTime>(),
                    new DockerReportedConfig("edgeAgent:v1", string.Empty, edgeAgentHash)));

            var runtimeInfoProvider = Mock.Of<IRuntimeInfoProvider>(r => r.GetModules(CancellationToken.None) == Task.FromResult(moduleRuntimeInfoList.AsEnumerable()));
            var moduleStateStore = new Mock<IEntityStore<string, ModuleState>>();
            moduleStateStore.Setup(m => m.Get("module1")).ReturnsAsync(Option.Some(new ModuleState(1, new DateTime(2017, 10, 13))));
            moduleStateStore.Setup(m => m.Get("module2")).ReturnsAsync(Option.Some(new ModuleState(2, new DateTime(2017, 10, 13))));
            moduleStateStore.Setup(m => m.Get("edgeHub")).ReturnsAsync(Option.Some(new ModuleState(3, new DateTime(2017, 10, 13))));
            moduleStateStore.Setup(m => m.Get("edgeAgent")).ReturnsAsync(Option.Some(new ModuleState(4, new DateTime(2017, 10, 13))));

            string minDockerVersion = "20";
            string dockerLoggingOptions = "dummy logging options";

            var module1 = new DockerModule("module1", "v1", ModuleStatus.Stopped, RestartPolicy.Always, new DockerConfig("mod1:v1", "{\"Env\":[\"foo=bar\"]}"), ImagePullPolicy.OnCreate, new ConfigurationInfo(), null);
            var module2 = new DockerModule("module2", "v2", ModuleStatus.Running, RestartPolicy.OnUnhealthy, new DockerConfig("mod2:v2", "{\"Env\":[\"foo2=bar2\"]}"), ImagePullPolicy.Never, new ConfigurationInfo(), null);
            var edgeHubModule = new EdgeHubDockerModule("docker", ModuleStatus.Running, RestartPolicy.Always, new DockerConfig("edgehub:v1", "{\"Env\":[\"foo3=bar3\"]}"), ImagePullPolicy.OnCreate, new ConfigurationInfo(), null);
            var edgeAgentModule = new EdgeAgentDockerModule("docker", new DockerConfig("edgeAgent:v1", string.Empty), ImagePullPolicy.OnCreate, new ConfigurationInfo(), null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                new DockerRuntimeInfo("docker", new DockerRuntimeConfig(minDockerVersion, dockerLoggingOptions)),
                new SystemModules(edgeAgentModule, edgeHubModule),
                new Dictionary<string, IModule> { [module1.Name] = module1, [module2.Name] = module2 });

            var environment = new DockerEnvironment(runtimeInfoProvider, deploymentConfig, moduleStateStore.Object, restartPolicyManager.Object, OperatingSystemType, Architecture, Version);

            // Act
            ModuleSet moduleSet = await environment.GetModulesAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(moduleSet);
            Assert.True(moduleSet.Modules.TryGetValue("module1", out IModule receivedModule1));
            Assert.True(moduleSet.Modules.TryGetValue("module2", out IModule receivedModule2));
            Assert.True(moduleSet.Modules.TryGetValue("edgeHub", out IModule receivedEdgeHub));
            Assert.True(moduleSet.Modules.TryGetValue("edgeAgent", out IModule receivedEdgeAgent));

            var receivedDockerModule1 = receivedModule1 as DockerRuntimeModule;
            Assert.NotNull(receivedDockerModule1);
            Assert.Equal("module1", receivedDockerModule1.Name);
            Assert.Equal("v1", receivedDockerModule1.Version);
            Assert.Equal(ModuleStatus.Stopped, receivedDockerModule1.DesiredStatus);
            Assert.Equal(RestartPolicy.Always, receivedDockerModule1.RestartPolicy);
            Assert.Equal(ImagePullPolicy.OnCreate, receivedDockerModule1.ImagePullPolicy);
            Assert.Equal("mod1:v1", receivedDockerModule1.Config.Image);
            Assert.Equal("{\"Env\":[\"foo=bar\"]}", JsonConvert.SerializeObject(receivedDockerModule1.Config.CreateOptions));
            Assert.Equal(ModuleStatus.Stopped, receivedDockerModule1.RuntimeStatus);
            Assert.Equal("dummy1", receivedDockerModule1.StatusDescription);
            Assert.Equal(0, receivedDockerModule1.ExitCode);
            Assert.Equal(new DateTime(2017, 10, 10), receivedDockerModule1.LastStartTimeUtc);
            Assert.Equal(DateTime.MinValue, receivedDockerModule1.LastExitTimeUtc);
            Assert.Equal(new DateTime(2017, 10, 13), receivedDockerModule1.LastRestartTimeUtc);
            Assert.Equal(module1Hash, (receivedDockerModule1.Config as DockerReportedConfig)?.ImageHash);
            Assert.Equal(1, receivedDockerModule1.RestartCount);

            var receivedDockerModule2 = receivedModule2 as DockerRuntimeModule;
            Assert.NotNull(receivedDockerModule2);
            Assert.Equal("module2", receivedDockerModule2.Name);
            Assert.Equal("v2", receivedDockerModule2.Version);
            Assert.Equal(ModuleStatus.Running, receivedDockerModule2.DesiredStatus);
            Assert.Equal(RestartPolicy.OnUnhealthy, receivedDockerModule2.RestartPolicy);
            Assert.Equal(ImagePullPolicy.Never, receivedDockerModule2.ImagePullPolicy);
            Assert.Equal("mod2:v2", receivedDockerModule2.Config.Image);
            Assert.Equal("{\"Env\":[\"foo2=bar2\"]}", JsonConvert.SerializeObject(receivedDockerModule2.Config.CreateOptions));
            Assert.Equal(ModuleStatus.Failed, receivedDockerModule2.RuntimeStatus);
            Assert.Equal("dummy2", receivedDockerModule2.StatusDescription);
            Assert.Equal(5, receivedDockerModule2.ExitCode);
            Assert.Equal(new DateTime(2017, 10, 12), receivedDockerModule2.LastStartTimeUtc);
            Assert.Equal(new DateTime(2017, 10, 14), receivedDockerModule2.LastExitTimeUtc);
            Assert.Equal(new DateTime(2017, 10, 13), receivedDockerModule2.LastRestartTimeUtc);
            Assert.Equal(module2Hash, (receivedDockerModule2.Config as DockerReportedConfig)?.ImageHash);
            Assert.Equal(2, receivedDockerModule2.RestartCount);

            var receivedDockerEdgeHub = receivedEdgeHub as EdgeHubDockerRuntimeModule;
            Assert.NotNull(receivedDockerEdgeHub);
            Assert.Equal("edgeHub", receivedDockerEdgeHub.Name);
            Assert.Equal(string.Empty, receivedDockerEdgeHub.Version);
            Assert.Equal(ModuleStatus.Running, receivedDockerEdgeHub.DesiredStatus);
            Assert.Equal(RestartPolicy.Always, receivedDockerEdgeHub.RestartPolicy);
            Assert.Equal(ImagePullPolicy.OnCreate, receivedDockerEdgeHub.ImagePullPolicy);
            Assert.Equal("edgehub:v1", receivedDockerEdgeHub.Config.Image);
            Assert.Equal("{\"Env\":[\"foo3=bar3\"]}", JsonConvert.SerializeObject(receivedDockerEdgeHub.Config.CreateOptions));
            Assert.Equal(ModuleStatus.Running, receivedDockerEdgeHub.RuntimeStatus);
            Assert.Equal(string.Empty, receivedDockerEdgeHub.StatusDescription);
            Assert.Equal(0, receivedDockerEdgeHub.ExitCode);
            Assert.Equal(new DateTime(2017, 10, 10), receivedDockerEdgeHub.LastStartTimeUtc);
            Assert.Equal(DateTime.MinValue, receivedDockerEdgeHub.LastExitTimeUtc);
            Assert.Equal(new DateTime(2017, 10, 13), receivedDockerEdgeHub.LastRestartTimeUtc);
            Assert.Equal(edgeHubHash, (receivedDockerEdgeHub.Config as DockerReportedConfig)?.ImageHash);
            Assert.Equal(3, receivedDockerEdgeHub.RestartCount);

            var receivedDockerEdgeAgent = receivedEdgeAgent as EdgeAgentDockerRuntimeModule;
            Assert.NotNull(receivedDockerEdgeAgent);
            Assert.Equal("edgeAgent", receivedDockerEdgeAgent.Name);
            Assert.Equal(string.Empty, receivedDockerEdgeAgent.Version);
            Assert.Equal(ModuleStatus.Running, receivedDockerEdgeAgent.RuntimeStatus);
            Assert.Equal(ImagePullPolicy.OnCreate, receivedDockerEdgeAgent.ImagePullPolicy);
            Assert.Equal("edgeAgent:v1", receivedDockerEdgeAgent.Config.Image);
            Assert.Equal("{}", JsonConvert.SerializeObject(receivedDockerEdgeAgent.Config.CreateOptions));
            Assert.Equal(new DateTime(2017, 10, 10), receivedDockerEdgeAgent.LastStartTimeUtc);
            Assert.Equal(edgeAgentHash, (receivedDockerEdgeAgent.Config as DockerReportedConfig)?.ImageHash);
        }
    }
}
