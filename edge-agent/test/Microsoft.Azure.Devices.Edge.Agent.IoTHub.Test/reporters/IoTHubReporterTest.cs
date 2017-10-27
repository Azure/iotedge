// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Reporters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
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
            var agentStateSerde = new Mock<ISerde<AgentState>>();

            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(null, environment.Object, agentStateSerde.Object));
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(edgeAgentConnection.Object, null, agentStateSerde.Object));
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(edgeAgentConnection.Object, environment.Object, null));
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
                var agentStateSerde = new Mock<ISerde<AgentState>>();

                edgeAgentConnection.SetupGet(c => c.ReportedProperties)
                    .Returns(Option.None<TwinCollection>());

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);
                await reporter.ReportAsync(cts.Token, ModuleSet.Empty, new DeploymentConfigInfo(0, DeploymentConfig.Empty), DeploymentStatus.Success);

                // Assert
                edgeAgentConnection.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            }
        }

        IEdgeAgentModule CreateMockEdgeAgentModule() => new TestAgentModule(
            Constants.EdgeAgentModuleName, "docker",
            new TestConfig("EdgeAgentImage"), new Core.ConfigurationInfo()
        );

        IEdgeHubModule CreateMockEdgeHubModule() => new TestHubModule(
            Constants.EdgeHubModuleName, "docker", ModuleStatus.Running,
            new TestConfig("EdgeAgentImage"), RestartPolicy.Always,
            new Core.ConfigurationInfo()
        );

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
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

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
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
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

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);
                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
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
                    systemModules = new
                    {
                        edgeAgent = new
                        {
                            type = "docker",
                            version = null as string,
                            status = null as string,
                            restartPolicy = null as string,
                            settings = new
                            {
                                image = "EdgeAgentImage"
                            }
                        }
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
                    coll["$version"] = 42;
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
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
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

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);
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
                    systemModules = new
                    {
                        edgeAgent = new
                        {
                            type = "docker",
                            version = null as string,
                            status = null as string,
                            restartPolicy = null as string,
                            settings = new
                            {
                                image = "EdgeAgentImage"
                            }
                        }
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
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                IEdgeHubModule edgeHubModule = this.CreateMockEdgeHubModule();
                var environment = new Mock<IEnvironment>();
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

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);

                // this should cause "extra_mod" to get deleted and "mod1" and "mod2" to get updated
                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // now change "current" so that "mod1" fails
                reportedState = new AgentState(
                    reportedState.LastDesiredVersion,
                    reportedState.LastDesiredStatus,
                    reportedState.RuntimeInfo,
                    new SystemModules(edgeAgentModule, edgeHubModule),
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

        [Fact]
        [Unit]
        public async void ReportedPatchIncludesEdgeHubInSystemModulesTest()
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
                var reportedState = AgentState.Empty;
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var edgeHubDesiredModule = new EdgeHubDockerModule(
                    "docker", ModuleStatus.Running, RestartPolicy.Always,
                    new DockerConfig("edge.azurecr.io/edgeHub:1.0"), new Core.ConfigurationInfo("1")
                );
                var edgeAgentDesiredModule = new EdgeAgentDockerModule(
                    "docker", new DockerConfig("edge.azurecr.io/edgeAgent:1.0"),
                    new Core.ConfigurationInfo("1")
                );
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(edgeAgentDesiredModule, edgeHubDesiredModule),
                    new Dictionary<string, IModule>()
                );
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig
                );

                // prepare IEnvironment mock
                var environment = new Mock<IEnvironment>();
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                environment.Setup(e => e.GetEdgeAgentModuleAsync(cts.Token)).Returns(Task.FromResult(edgeAgentModule));
                environment.Setup(e => e.GetUpdatedRuntimeInfoAsync(deploymentConfigInfo.DeploymentConfig.Runtime))
                    .ReturnsAsync(new DockerReportedRuntimeInfo(
                        RuntimeType,
                        (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo).Config,
                        new DockerPlatformInfo(OperatingSystemType, Architecture))
                    );

                // build current module set
                var edgeHubRuntimeMmodule = new EdgeHubDockerRuntimeModule(
                    Constants.EdgeHubModuleName, "1.0", ModuleStatus.Running, RestartPolicy.Always,
                    new DockerConfig("edge.azurecr.io/edgeHub:1.0"), 0, string.Empty,
                    DateTime.UtcNow - TimeSpan.FromHours(1), DateTime.MinValue,
                    0, DateTime.MinValue, ModuleStatus.Running, new Core.ConfigurationInfo("1")
                );
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
                    ),
                    edgeHubRuntimeMmodule
                );

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);
                await reporter.ReportAsync(cts.Token, currentModuleSet, deploymentConfigInfo, DeploymentStatus.Success);

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
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
                    systemModules = new
                    {
                        edgeHub = edgeHubRuntimeMmodule,
                        edgeAgent = new
                        {
                            type = "docker",
                            version = null as string,
                            status = null as string,
                            restartPolicy = null as string,
                            settings = new
                            {
                                image = "EdgeAgentImage"
                            }
                        }
                    },
                    modules = new
                    {
                        mod1 = currentModuleSet.Modules["mod1"],
                        mod2 = currentModuleSet.Modules["mod2"]
                    }
                });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportAsyncAcceptsNullInputs()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = AgentState.Empty;
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare IEnvironment mock
                var environment = new Mock<IEnvironment>();
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                environment.Setup(e => e.GetEdgeAgentModuleAsync(cts.Token)).Returns(Task.FromResult(edgeAgentModule));

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);
                await reporter.ReportAsync(cts.Token, null, null, DeploymentStatus.Success);

                // Assert
                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(new
                {
                    lastDesiredStatus = new
                    {
                        code = (int)DeploymentStatusCode.Successful
                    },
                    systemModules = new
                    {
                        edgeAgent = new
                        {
                            type = "docker",
                            version = null as string,
                            status = null as string,
                            restartPolicy = null as string,
                            settings = new
                            {
                                image = "EdgeAgentImage"
                            }
                        }
                    }
                });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportAsyncReportsErrorIfInitialDeserializeFails()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = AgentState.Empty;
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Throws(new FormatException("Bad format"));

                var environment = new Mock<IEnvironment>();

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, environment.Object, agentStateSerde.Object);
                await reporter.ReportAsync(cts.Token, ModuleSet.Empty, new DeploymentConfigInfo(0, DeploymentConfig.Empty), DeploymentStatus.Success);

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(new
                {
                    lastDesiredVersion = 0,
                    lastDesiredStatus = new
                    {
                        code = (int)DeploymentStatusCode.Failed,
                        description = "Bad format"
                    }
                });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }
    }
}
