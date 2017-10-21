// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Reporters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class IoTHubReporterTest
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Fact]
        [Unit]
        public void CreateInvalidInputs()
        {
            var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
            var environment = new Mock<IEnvironment>();

            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(null, environment.Object));
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(edgeAgentConnection.Object, null));
        }

        [Fact]
        [Unit]
        public async void SkipReportIfNoSavedStateAndNoStateFromConfigSource()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var environment = new Mock<IEnvironment>();
                edgeAgentConnection.Setup(c => c.ReportedProperties)
                    .Returns(Option.None<TwinCollection>());

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object);
                await reporter.ReportAsync(cts.Token, ModuleSet.Empty, new DeploymentConfigInfo(0, DeploymentConfig.Empty), DeploymentStatus.Success);

                // Assert
                edgeAgentConnection.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            }
        }

        IModule CreateMockEdgeAgentModule() => new TestRuntimeModule(
                            Constants.EdgeAgentModuleName, string.Empty, RestartPolicy.Always, "docker", ModuleStatus.Running,
                            new TestConfig("EdgeAgentImage"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Running);

        [Fact]
        [Unit]
        public async void ReportedPatchTest()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState
                (
                    0, DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                            new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Running
                        ),
                        new TestRuntimeModule(
                            "extra_mod", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                            new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Backoff
                        )
                    ).Modules.ToImmutableDictionary()
                );
                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(null, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig
                );

                // prepare IEnvironment mock
                var environment = new Mock<IEnvironment>();
                IModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                environment.Setup(e => e.GetEdgeAgentModuleAsync(cts.Token)).ReturnsAsync(edgeAgentModule);
                environment.Setup(e => e.GetUpdatedRuntimeInfoAsync(deploymentConfigInfo.DeploymentConfig.Runtime))
                    .ReturnsAsync(new DockerReportedRuntimeInfo(
                        RuntimeType,
                        (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo).Config,
                        new DockerPlatformInfo(OperatingSystemType, Architecture))
                    );

                // build current module set
                var currentModuleSet = ModuleSet.Create(
                    new TestRuntimeModule(
                        "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Backoff
                    ),
                    new TestRuntimeModule(
                        "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Running
                    )
                );

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object);
                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(new
                {
                    lastDesiredVersion = DesiredVersion,
                    lastDesiredStatus = new
                    {
                        code = (int)DeploymentStatusCode.Successful
                    },
                    runtime = new
                    {
                        type = RuntimeType,
                        settings = new
                        {
                            minDockerVersion = MinDockerVersion,
                            loggingOptions = LoggingOptions
                        },
                        platform = new
                        {
                            os = OperatingSystemType,
                            architecture = Architecture
                        }
                    },
                    systemModules = new Dictionary<string, object>
                    {
                        { "edgeAgent", edgeAgentModule }
                    },
                    modules = new Dictionary<string, object>
                    {
                        {
                            currentModuleSet.Modules["mod1"].Name,
                            new
                            {
                                runtimeStatus = "backoff"
                            }
                        },
                        { currentModuleSet.Modules["mod2"].Name, currentModuleSet.Modules["mod2"] },
                        { "extra_mod", null }
                    }
                });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportedPatchTestStripMetadata()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState
                (
                    0, DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                            new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Running
                        ),
                        new TestRuntimeModule(
                            "extra_mod", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                            new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Backoff
                        )
                    ).Modules.ToImmutableDictionary()
                );

                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(() =>
                {
                    var coll = new TwinCollection(JsonConvert.SerializeObject(reportedState));
                    coll["$metadata"] = JObject.FromObject(new { foo = 10 });
                    coll["$version"] = 751;
                    return Option.Some(coll);
                });

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(null, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig
                );

                // prepare IEnvironment mock
                var environment = new Mock<IEnvironment>();
                IModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                environment.Setup(e => e.GetEdgeAgentModuleAsync(cts.Token)).Returns(Task.FromResult(edgeAgentModule));
                environment.Setup(e => e.GetUpdatedRuntimeInfoAsync(deploymentConfigInfo.DeploymentConfig.Runtime))
                    .ReturnsAsync(new DockerReportedRuntimeInfo(
                        RuntimeType,
                        (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo).Config,
                        new DockerPlatformInfo(OperatingSystemType, Architecture))
                    );

                // build current module set
                var currentModuleSet = ModuleSet.Create(
                    new TestRuntimeModule(
                        "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Backoff
                    ),
                    new TestRuntimeModule(
                        "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Running
                    )
                );

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object);
                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(new
                {
                    lastDesiredVersion = DesiredVersion,
                    lastDesiredStatus = new
                    {
                        code = (int)DeploymentStatusCode.Successful
                    },
                    runtime = new
                    {
                        type = RuntimeType,
                        settings = new
                        {
                            minDockerVersion = MinDockerVersion,
                            loggingOptions = LoggingOptions
                        },
                        platform = new
                        {
                            os = OperatingSystemType,
                            architecture = Architecture
                        }
                    },
                    systemModules = new Dictionary<string, object>
                    {
                        { "edgeAgent", edgeAgentModule }
                    },
                    modules = new Dictionary<string, object>
                    {
                        {
                            currentModuleSet.Modules["mod1"].Name,
                            new
                            {
                                runtimeStatus = "backoff"
                            }
                        },
                        { currentModuleSet.Modules["mod2"].Name, currentModuleSet.Modules["mod2"] },
                        { "extra_mod", null }
                    }
                });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportedPatchTest2()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState
                (
                    0, DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                            new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Running
                        ),
                        new TestRuntimeModule(
                            "extra_mod", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                            new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                            0, DateTime.MinValue, ModuleStatus.Backoff
                        )
                    ).Modules.ToImmutableDictionary()
                );
                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(null, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig
                );

                // prepare IEnvironment mock
                var environment = new Mock<IEnvironment>();
                IModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                environment.Setup(e => e.GetEdgeAgentModuleAsync(cts.Token)).Returns(Task.FromResult(edgeAgentModule));
                environment.Setup(e => e.GetUpdatedRuntimeInfoAsync(deploymentConfigInfo.DeploymentConfig.Runtime))
                    .ReturnsAsync(new DockerReportedRuntimeInfo(
                        RuntimeType,
                        (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo).Config,
                        new DockerPlatformInfo(OperatingSystemType, Architecture))
                    );

                // build current module set
                var currentModuleSet = ModuleSet.Create(
                    new TestRuntimeModule(
                        "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Backoff
                    ),
                    new TestRuntimeModule(
                        "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Running
                    )
                );

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object);

                // this should cause "extra_mod" to get deleted and "mod1" and "mod2" to get updated
                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // now change "current" so that "mod1" fails
                reportedState = new AgentState(
                    reportedState.LastDesiredVersion,
                    reportedState.LastDesiredStatus,
                    reportedState.RuntimeInfo,
                    new Dictionary<string, IModule>
                    {
                        { Constants.EdgeAgentModuleName, edgeAgentModule }
                    },
                    currentModuleSet.Modules.ToImmutableDictionary()
                );
                currentModuleSet = ModuleSet.Create(
                    new TestRuntimeModule(
                        "mod1", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Failed
                    ),
                    new TestRuntimeModule(
                        "mod2", "1.0", RestartPolicy.OnUnhealthy, "test", ModuleStatus.Running,
                        new TestConfig("image1"), 0, string.Empty, DateTime.MinValue, DateTime.MinValue,
                        0, DateTime.MinValue, ModuleStatus.Running
                    )
                );

                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(new
                {
                    modules = new Dictionary<string, object>
                    {
                        {
                            currentModuleSet.Modules["mod1"].Name,
                            new
                            {
                                runtimeStatus = "failed"
                            }
                        }
                    }
                });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }
    }
}
