// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Reporters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
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
    using ConfigurationInfo = Microsoft.Azure.Devices.Edge.Agent.Core.ConfigurationInfo;

    public class IoTHubReporterTest
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Fact]
        [Unit]
        public void CreateInvalidInputs()
        {
            var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
            var agentStateSerde = new Mock<ISerde<AgentState>>();
            var versionInfo = new VersionInfo("v1", "b1", "c1");

            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(null, agentStateSerde.Object, versionInfo));
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(edgeAgentConnection.Object, null, versionInfo));
            Assert.Throws<ArgumentNullException>(() => new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, null));
            Assert.NotNull(new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo));
        }

        [Fact]
        [Unit]
        public async void SkipReportIfNoSavedStateAndNoStateFromConfigSource()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var agentStateSerde = new Mock<ISerde<AgentState>>();
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                edgeAgentConnection.SetupGet(c => c.ReportedProperties)
                    .Returns(Option.None<TwinCollection>());

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, ModuleSet.Empty, Mock.Of<IRuntimeInfo>(), 0, Option.Some(DeploymentStatus.Success));

                // Assert
                edgeAgentConnection.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            }
        }

        [Fact]
        [Unit]
        public async void ClearAndGenerateNewReportedInfoIfDeserializeFails()
        {
            // Arrange
            using (var cts = new CancellationTokenSource(Timeout))
            {
                const string SchemaVersion = "1.0";
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";
                const string Version = "17.11.0-ce";

                var versionInfo = new VersionInfo("v1", "b1", "c1");
                // Mock IEdgeAgentConnection
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "extra_mod",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                var patches = new List<TwinCollection>();
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patches.Add(tc))
                    .Returns(Task.CompletedTask);

                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(null, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // Mock AgentStateSerDe
                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Throws(new FormatException("Bad format"));

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.Equal(2, patches.Count);
                JObject patch1Json = JObject.Parse(patches[0].ToJson());
                foreach (KeyValuePair<string, JToken> keyValuePair in patch1Json)
                {
                    Assert.Equal(JTokenType.Null, keyValuePair.Value.Type);
                }

                JObject patch2Json = JObject.Parse(patches[1].ToJson());
                JObject expectedPatch2Json = JObject.FromObject(
                    new
                    {
                        schemaVersion = SchemaVersion,
                        version = new
                        {
                            version = versionInfo.Version,
                            build = versionInfo.Build,
                            commit = versionInfo.Commit
                        },
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
                                loggingOptions = LoggingOptions,
                                registryCredentials = new { }
                            },
                            platform = new
                            {
                                os = OperatingSystemType,
                                architecture = Architecture,
                                version = Version
                            }
                        },
                        systemModules = new
                        {
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "EdgeAgentImage"
                                },
                                imagePullPolicy = "on-create"
                            }
                        },
                        modules = new Dictionary<string, object>
                        {
                            { currentModuleSet.Modules["mod1"].Name, currentModuleSet.Modules["mod1"] },
                            { currentModuleSet.Modules["mod2"].Name, currentModuleSet.Modules["mod2"] },
                        }
                    });
                Assert.True(JToken.DeepEquals(expectedPatch2Json, patch2Json));
            }
        }

        [Fact]
        [Unit]
        public async void ReportedPatchTest()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const string SchemaVersion = "1.0";
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "extra_mod",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
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
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        schemaVersion = SchemaVersion,
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
                                loggingOptions = LoggingOptions,
                                registryCredentials = new { }
                            },
                            platform = new
                            {
                                os = OperatingSystemType,
                                architecture = Architecture,
                                version = Version
                            }
                        },
                        systemModules = new
                        {
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "EdgeAgentImage"
                                },
                                imagePullPolicy = "on-create"
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
        public async void ReportedPatchNoneStatusTest()
        {
            using (var cts = new CancellationTokenSource())
            {
                // Arrange
                const string SchemaVersion = "1.0";
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "extra_mod",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
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
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        schemaVersion = SchemaVersion,
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
                                loggingOptions = LoggingOptions,
                                registryCredentials = new { }
                            },
                            platform = new
                            {
                                os = OperatingSystemType,
                                architecture = Architecture,
                                version = Version
                            }
                        },
                        systemModules = new
                        {
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "EdgeAgentImage"
                                },
                                imagePullPolicy = "on-create"
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

                // Arrange
                patch = null;

                // Act
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.None<DeploymentStatus>());

                // Assert
                Assert.Null(patch);
            }
        }

        [Fact]
        [Unit]
        public async void ReportedPatchTestStripMetadata()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const string SchemaVersion = "1.0";
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "extra_mod",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);

                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(
                    () =>
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
                    deploymentConfig);

                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, deploymentConfigInfo.Version, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.NotNull(patch);

                var patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        schemaVersion = SchemaVersion,
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
                                loggingOptions = LoggingOptions,
                                registryCredentials = new { }
                            },
                            platform = new
                            {
                                os = OperatingSystemType,
                                architecture = Architecture,
                                version = Version
                            }
                        },
                        systemModules = new
                        {
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "EdgeAgentImage"
                                },
                                imagePullPolicy = "on-create"
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
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "extra_mod",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
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
                    deploymentConfig);

                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);

                // this should cause "extra_mod" to get deleted and "mod1" and "mod2" to get updated
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // now change "current" so that "mod1" fails
                currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Failed),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.NotNull(patch);

                var patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(
                    new
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
        public async void ReportAsyncDoesNotReportIfPatchIsEmpty()
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
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    DesiredVersion,
                    DeploymentStatus.Success,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "mod2",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(null, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);

                // this should cause a patch to get generated
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // since nothing changed, this call should not cause a patch to be generated
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                edgeAgentConnection.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Once);
            }
        }

        [Fact]
        [Unit]
        public async void ReportedPatchIncludesEdgeHubInSystemModulesTest()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const string SchemaVersion = "1.0";
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "logging options";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                AgentState reportedState = AgentState.Empty;
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var edgeHubDesiredModule = new EdgeHubDockerModule(
                    "docker",
                    ModuleStatus.Running,
                    RestartPolicy.Always,
                    new DockerConfig("edge.azurecr.io/edgeHub:1.0"),
                    ImagePullPolicy.Never,
                    new ConfigurationInfo("1"),
                    new Dictionary<string, EnvVal>());
                var edgeAgentDesiredModule = new EdgeAgentDockerModule(
                    "docker",
                    new DockerConfig("edge.azurecr.io/edgeAgent:1.0"),
                    ImagePullPolicy.OnCreate,
                    new ConfigurationInfo("1"),
                    new Dictionary<string, EnvVal>());
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(edgeAgentDesiredModule, edgeHubDesiredModule),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig);

                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();
                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // build current module set
                DateTime lastStartTimeUtc = DateTime.Parse(
                    "2017-11-13T23:44:35.127381Z",
                    null,
                    DateTimeStyles.RoundtripKind);
                var edgeHubRuntimeModule = new EdgeHubDockerRuntimeModule(
                    ModuleStatus.Running,
                    RestartPolicy.Always,
                    new DockerConfig("edge.azurecr.io/edgeHub:1.0"),
                    0,
                    string.Empty,
                    lastStartTimeUtc,
                    DateTime.MinValue,
                    0,
                    DateTime.MinValue,
                    ModuleStatus.Running,
                    ImagePullPolicy.Never,
                    new ConfigurationInfo("1"),
                    new Dictionary<string, EnvVal> { ["foo"] = new EnvVal("Bar") });
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff,
                        ImagePullPolicy.OnCreate),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running,
                        ImagePullPolicy.Never),
                    edgeHubRuntimeModule);

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        schemaVersion = SchemaVersion,
                        version = new
                        {
                            version = versionInfo.Version,
                            build = versionInfo.Build,
                            commit = versionInfo.Commit
                        },
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
                                loggingOptions = LoggingOptions,
                                registryCredentials = new { }
                            },
                            platform = new
                            {
                                os = OperatingSystemType,
                                architecture = Architecture,
                                version = Version
                            }
                        },
                        systemModules = new
                        {
                            edgeHub = edgeHubRuntimeModule,
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "EdgeAgentImage"
                                },
                                imagePullPolicy = "on-create"
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
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                AgentState reportedState = AgentState.Empty;
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

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, null, null, -1, Option.Some(DeploymentStatus.Success));

                // Assert
                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        schemaVersion = "1.0",
                        lastDesiredStatus = new
                        {
                            code = (int)DeploymentStatusCode.Successful
                        },
                        version = new
                        {
                            version = versionInfo.Version,
                            build = versionInfo.Build,
                            commit = versionInfo.Commit
                        }
                    });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportEmptyShutdown()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var agentStateSerde = new Mock<ISerde<AgentState>>();
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);

                // Now use the last reported configuration to report a shutdown
                await reporter.ReportShutdown(DeploymentStatus.Success, cts.Token);

                // Assert
                Assert.Null(patch);
            }
        }

        [Fact]
        [Unit]
        public async void ReportShutdown()
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
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");
                DateTime lastStartTimeUtc = DateTime.Parse("2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind);

                IEdgeAgentModule edgeAgent = new EdgeAgentDockerRuntimeModule(new DockerReportedConfig("image", string.Empty, "hash"), ModuleStatus.Running, 0, string.Empty, lastStartTimeUtc, DateTime.MinValue, ImagePullPolicy.OnCreate, new ConfigurationInfo("id"), new Dictionary<string, EnvVal>());
                IEdgeHubModule edgeHub = new EdgeHubDockerRuntimeModule(ModuleStatus.Running, RestartPolicy.Always, new DockerReportedConfig("hubimage", string.Empty, "hash"), 0, string.Empty, DateTime.Now, DateTime.Now, 0, DateTime.Now, ModuleStatus.Running, ImagePullPolicy.OnCreate, new ConfigurationInfo("hub"), new Dictionary<string, EnvVal>());

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Empty.Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(edgeAgent, edgeHub),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgent,
                    edgeHub,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);

                // this should cause all modules to be updated.
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Now use the last reported configuration to report a shutdown
                await reporter.ReportShutdown(DeploymentStatus.Success, cts.Token);

                // Assert
                Assert.NotNull(patch);

                var patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        systemModules = new Dictionary<string, object>
                        {
                            {
                                edgeAgent.Name,
                                new
                                {
                                    runtimeStatus = "unknown"
                                }
                            },
                            {
                                edgeHub.Name,
                                new
                                {
                                    runtimeStatus = "unknown"
                                }
                            }
                        },
                        modules = new Dictionary<string, object>
                        {
                            {
                                currentModuleSet.Modules["mod1"].Name,
                                new
                                {
                                    runtimeStatus = "unknown"
                                }
                            },
                            {
                                currentModuleSet.Modules["mod2"].Name,
                                new
                                {
                                    runtimeStatus = "unknown"
                                }
                            }
                        }
                    });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportShutdownWithOnlyEdgeAgentInState()
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
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");
                DateTime lastStartTimeUtc = DateTime.Parse("2017-11-13T23:44:35.127381Z", null, DateTimeStyles.RoundtripKind);

                IEdgeAgentModule edgeAgent = new EdgeAgentDockerRuntimeModule(new DockerReportedConfig("image", string.Empty, "hash"), ModuleStatus.Running, 0, string.Empty, lastStartTimeUtc, DateTime.MinValue, ImagePullPolicy.OnCreate, new ConfigurationInfo("id"), new Dictionary<string, EnvVal>());

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    new SystemModules(edgeAgent, null),
                    ModuleSet.Empty.Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
                edgeAgentConnection.SetupGet(c => c.ReportedProperties).Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, LoggingOptions)),
                    new SystemModules(edgeAgent, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(edgeAgent);

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);

                // this should cause all modules to be updated.
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Now use the last reported configuration to report a shutdown
                await reporter.ReportShutdown(DeploymentStatus.Success, cts.Token);

                // Assert
                Assert.NotNull(patch);

                var patchJson = JsonConvert.DeserializeObject(patch.ToJson()) as JObject;
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        systemModules = new Dictionary<string, object>
                        {
                            {
                                edgeAgent.Name,
                                new
                                {
                                    runtimeStatus = "unknown"
                                }
                            }
                        }
                    });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        [Fact]
        [Unit]
        public async void ReportedPatchWithEnvVarsTest()
        {
            using (var cts = new CancellationTokenSource(Timeout))
            {
                // Arrange
                const string SchemaVersion = "1.0";
                const long DesiredVersion = 10;
                const string RuntimeType = "docker";
                const string MinDockerVersion = "1.25";
                const string LoggingOptions = "";
                const string OperatingSystemType = "linux";
                const string Architecture = "x86_x64";
                const string Version = "17.11.0-ce";
                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // prepare IEdgeAgentConnection mock
                var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
                var reportedState = new AgentState(
                    0,
                    DeploymentStatus.Unknown,
                    null,
                    null,
                    ModuleSet.Create(
                        new TestRuntimeModule(
                            "mod1",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Running),
                        new TestRuntimeModule(
                            "extra_mod",
                            "1.0",
                            RestartPolicy.OnUnhealthy,
                            "test",
                            ModuleStatus.Running,
                            new TestConfig("image1"),
                            0,
                            string.Empty,
                            DateTime.MinValue,
                            DateTime.MinValue,
                            0,
                            DateTime.MinValue,
                            ModuleStatus.Backoff)).Modules.ToImmutableDictionary(),
                    string.Empty,
                    versionInfo);
                edgeAgentConnection
                    .SetupGet(c => c.ReportedProperties)
                    .Returns(Option.Some(new TwinCollection(JsonConvert.SerializeObject(reportedState))));

                TwinCollection patch = null;
                edgeAgentConnection.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                    .Callback<TwinCollection>(tc => patch = tc)
                    .Returns(Task.CompletedTask);

                // prepare AgentConfig
                var registryCreds = new Dictionary<string, RegistryCredentials>
                {
                    ["r1"] = new RegistryCredentials("a1", "u1", "p1"),
                    ["r2"] = new RegistryCredentials("a2", "u2", "p2")
                };
                var deploymentConfig = new DeploymentConfig(
                    "1.0",
                    new DockerRuntimeInfo(RuntimeType, new DockerRuntimeConfig(MinDockerVersion, registryCreds)),
                    new SystemModules(null, null),
                    new Dictionary<string, IModule>());
                var deploymentConfigInfo = new DeploymentConfigInfo(
                    DesiredVersion,
                    deploymentConfig);

                IRuntimeInfo runtimeInfo = new DockerReportedRuntimeInfo(
                    RuntimeType,
                    (deploymentConfigInfo.DeploymentConfig.Runtime as DockerRuntimeInfo)?.Config,
                    new DockerPlatformInfo(OperatingSystemType, Architecture, Version));
                IEdgeAgentModule edgeAgentModule = this.CreateMockEdgeAgentModule();

                // build current module set
                ModuleSet currentModuleSet = ModuleSet.Create(
                    edgeAgentModule,
                    new TestRuntimeModule(
                        "mod1",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Backoff,
                        ImagePullPolicy.Never,
                        null,
                        new Dictionary<string, EnvVal> { ["e1"] = new EnvVal("e1Val") }),
                    new TestRuntimeModule(
                        "mod2",
                        "1.0",
                        RestartPolicy.OnUnhealthy,
                        "test",
                        ModuleStatus.Running,
                        new TestConfig("image1"),
                        0,
                        string.Empty,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        0,
                        DateTime.MinValue,
                        ModuleStatus.Running,
                        ImagePullPolicy.OnCreate,
                        null,
                        new Dictionary<string, EnvVal> { ["e2"] = new EnvVal("e2Val") }));

                var agentStateSerde = new Mock<ISerde<AgentState>>();
                agentStateSerde.Setup(s => s.Deserialize(It.IsAny<string>()))
                    .Returns(reportedState);

                // Act
                var reporter = new IoTHubReporter(edgeAgentConnection.Object, agentStateSerde.Object, versionInfo);
                await reporter.ReportAsync(cts.Token, currentModuleSet, runtimeInfo, DesiredVersion, Option.Some(DeploymentStatus.Success));

                // Assert
                Assert.NotNull(patch);

                JObject patchJson = JObject.Parse(patch.ToJson());
                JObject expectedPatchJson = JObject.FromObject(
                    new
                    {
                        schemaVersion = SchemaVersion,
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
                                loggingOptions = LoggingOptions,
                                registryCredentials = new
                                {
                                    r1 = new
                                    {
                                        address = "a1",
                                        username = "u1",
                                        password = "p1"
                                    },
                                    r2 = new
                                    {
                                        address = "a2",
                                        username = "u2",
                                        password = "p2"
                                    }
                                }
                            },
                            platform = new
                            {
                                os = OperatingSystemType,
                                architecture = Architecture,
                                version = Version
                            }
                        },
                        systemModules = new
                        {
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "EdgeAgentImage"
                                },
                                imagePullPolicy = "on-create"
                            }
                        },
                        modules = new Dictionary<string, object>
                        {
                            {
                                currentModuleSet.Modules["mod1"].Name,
                                new
                                {
                                    runtimeStatus = "backoff",
                                    env = new
                                    {
                                        e1 = new
                                        {
                                            value = "e1Val"
                                        }
                                    },
                                    imagePullPolicy = "never"
                                }
                            },
                            { currentModuleSet.Modules["mod2"].Name, currentModuleSet.Modules["mod2"] },
                            { "extra_mod", null }
                        }
                    });

                Assert.True(JToken.DeepEquals(expectedPatchJson, patchJson));
            }
        }

        IEdgeAgentModule CreateMockEdgeAgentModule() => new TestAgentModule(
            Constants.EdgeAgentModuleName,
            "docker",
            new TestConfig("EdgeAgentImage"),
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());
    }
}
