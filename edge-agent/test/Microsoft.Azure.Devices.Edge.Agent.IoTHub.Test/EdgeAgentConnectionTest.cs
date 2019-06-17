// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using ServiceClient = Microsoft.Azure.Devices.ServiceClient;

    public class EdgeAgentConnectionTest
    {
        const string DockerType = "docker";
        static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(60);

        public static async Task<Configuration> CreateConfigurationAsync(RegistryManager registryMananger, string configurationId, string targetCondition, int priority)
        {
            var configuration = new Configuration(configurationId)
            {
                Labels = new Dictionary<string, string>
                {
                    { "App", "Stream Analytics" }
                },
                Content = GetDefaultConfigurationContent(),
                Priority = priority,
                TargetCondition = targetCondition
            };

            return await registryMananger.AddConfigurationAsync(configuration);
        }

        public static TwinCollection GetEdgeAgentReportedProperties(DeploymentConfigInfo deploymentConfigInfo)
        {
            DeploymentConfig deploymentConfig = deploymentConfigInfo.DeploymentConfig;
            var reportedProperties = new
            {
                lastDesiredVersion = deploymentConfigInfo.Version,
                lastDesiredStatus = new
                {
                    code = 200
                },
                runtime = new
                {
                    type = "docker"
                },
                systemModules = new
                {
                    edgeAgent = new
                    {
                        runtimeStatus = "running",
                        description = "All good",
                        configuration = new
                        {
                            id = deploymentConfig.SystemModules.EdgeAgent.OrDefault().ConfigurationInfo.Id
                        }
                    },
                    edgeHub = new
                    {
                        runtimeStatus = "running",
                        description = "All good",
                        configuration = new
                        {
                            id = deploymentConfig.SystemModules.EdgeHub.OrDefault().ConfigurationInfo.Id
                        }
                    }
                },
                modules = new
                {
                    mongoserver = new
                    {
                        runtimeStatus = "running",
                        description = "All good",
                        configuration = new
                        {
                            id = deploymentConfig.Modules["mongoserver"].ConfigurationInfo.Id
                        }
                    },
                    asa = new
                    {
                        runtimeStatus = "running",
                        description = "All good",
                        configuration = new
                        {
                            id = deploymentConfig.Modules["asa"].ConfigurationInfo.Id
                        }
                    }
                }
            };

            string patch = JsonConvert.SerializeObject(reportedProperties);
            return new TwinCollection(patch);
        }

        public static ConfigurationContent GetDefaultConfigurationContent()
        {
            return new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetEdgeAgentConfiguration()
                    },
                    ["$edgeHub"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetEdgeHubConfiguration()
                    },
                    ["mongoserver"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetTwinConfiguration("mongoserver")
                    },
                    ["asa"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetTwinConfiguration("asa")
                    }
                }
            };
        }

        [Integration]
        [Fact]
        public async Task EdgeAgentConnectionBasicTest()
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };

            try
            {
                edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

                await SetAgentDesiredProperties(registryManager, edgeDeviceId);

                string edgeAgentConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};ModuleId=$edgeAgent;SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
                IModuleClientProvider moduleClientProvider = new ModuleClientProvider(
                    edgeAgentConnectionString,
                    new SdkModuleClientProvider(),
                    Option.None<UpstreamProtocol>(),
                    Option.None<IWebProxy>(),
                    Option.None<string>(),
                    false,
                    TimeSpan.FromDays(1));

                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerDesiredModule) }
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeAgentDockerModule) }
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeHubDockerModule) }
                };

                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerRuntimeInfo) }
                };

                var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
                {
                    [typeof(IModule)] = moduleDeserializerTypes,
                    [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                    [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                    [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
                };

                ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);
                IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
                IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout));
                await Task.Delay(TimeSpan.FromSeconds(10));

                Option<DeploymentConfigInfo> deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                Assert.True(deploymentConfigInfo.HasValue);
                DeploymentConfig deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
                Assert.NotNull(deploymentConfig);
                Assert.NotNull(deploymentConfig.Modules);
                Assert.NotNull(deploymentConfig.Runtime);
                Assert.NotNull(deploymentConfig.SystemModules);
                Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion.ToString(), deploymentConfig.SchemaVersion);
                Assert.Equal(1, deploymentConfig.Modules.Count);
                Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
                ValidateRuntimeConfig(deploymentConfig.Runtime);
                ValidateModules(deploymentConfig);

                await UpdateAgentDesiredProperties(registryManager, edgeDeviceId);
                await Task.Delay(TimeSpan.FromSeconds(10));

                deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                Assert.True(deploymentConfigInfo.HasValue);
                deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
                Assert.NotNull(deploymentConfig);
                Assert.NotNull(deploymentConfig.Modules);
                Assert.NotNull(deploymentConfig.Runtime);
                Assert.NotNull(deploymentConfig.SystemModules);
                Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion.ToString(), deploymentConfig.SchemaVersion);
                Assert.Equal(2, deploymentConfig.Modules.Count);
                Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
                Assert.NotNull(deploymentConfig.Modules["mlModule"]);
                ValidateRuntimeConfig(deploymentConfig.Runtime);
            }
            finally
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(edgeDevice);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        [Integration]
        [Fact]
        public async Task EdgeAgentConnectionConfigurationTest()
        {
            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid();
            string configurationId = "testconfiguration-" + Guid.NewGuid().ToString();
            string conditionPropertyName = "condition-" + Guid.NewGuid().ToString("N");
            string conditionPropertyValue = Guid.NewGuid().ToString();
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            try
            {
                await registryManager.OpenAsync();

                var edgeDevice = new Device(edgeDeviceId)
                {
                    Capabilities = new DeviceCapabilities { IotEdge = true },
                    Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
                };
                edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

                Twin twin = await registryManager.GetTwinAsync(edgeDeviceId);
                twin.Tags[conditionPropertyName] = conditionPropertyValue;
                await registryManager.UpdateTwinAsync(edgeDeviceId, twin, twin.ETag);
                await registryManager.GetTwinAsync(edgeDeviceId, "$edgeAgent");
                await registryManager.GetTwinAsync(edgeDeviceId, "$edgeHub");

                await CreateConfigurationAsync(registryManager, configurationId, $"tags.{conditionPropertyName}='{conditionPropertyValue}'", 10);

                // Service takes about 5 mins to sync config to twin
                await Task.Delay(TimeSpan.FromMinutes(7));

                string edgeAgentConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};ModuleId=$edgeAgent;SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
                IModuleClientProvider moduleClientProvider = new ModuleClientProvider(
                    edgeAgentConnectionString,
                    new SdkModuleClientProvider(),
                    Option.None<UpstreamProtocol>(),
                    Option.None<IWebProxy>(),
                    Option.None<string>(),
                    false,
                    TimeSpan.FromDays(1));

                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerDesiredModule) }
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeAgentDockerModule) }
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeHubDockerModule) }
                };

                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerRuntimeInfo) }
                };

                var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
                {
                    [typeof(IModule)] = moduleDeserializerTypes,
                    [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                    [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                    [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
                };

                ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);
                IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
                IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout));
                await Task.Delay(TimeSpan.FromSeconds(20));

                Option<DeploymentConfigInfo> deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                Assert.True(deploymentConfigInfo.HasValue);
                DeploymentConfig deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
                Assert.NotNull(deploymentConfig);
                Assert.NotNull(deploymentConfig.Modules);
                Assert.NotNull(deploymentConfig.Runtime);
                Assert.NotNull(deploymentConfig.SystemModules);
                Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion.ToString(), deploymentConfig.SchemaVersion);
                Assert.Equal(2, deploymentConfig.Modules.Count);
                Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
                Assert.NotNull(deploymentConfig.Modules["asa"]);

                TwinCollection reportedPatch = GetEdgeAgentReportedProperties(deploymentConfigInfo.OrDefault());
                await edgeAgentConnection.UpdateReportedPropertiesAsync(reportedPatch);

                // Service takes about 5 mins to sync statistics to config
                await Task.Delay(TimeSpan.FromMinutes(7));

                Configuration config = await registryManager.GetConfigurationAsync(configurationId);
                Assert.NotNull(config);
                Assert.NotNull(config.SystemMetrics);
                Assert.True(config.SystemMetrics.Results.ContainsKey("targetedCount"));
                Assert.Equal(1, config.SystemMetrics.Results["targetedCount"]);
                Assert.True(config.SystemMetrics.Results.ContainsKey("appliedCount"));
                Assert.Equal(1, config.SystemMetrics.Results["appliedCount"]);
            }
            finally
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(edgeDeviceId);
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    await DeleteConfigurationAsync(registryManager, configurationId);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncReturnsConfigWhenThereAreNoErrors()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(
                        JObject.FromObject(
                            new Dictionary<string, object>
                            {
                                { "$version", 10 },

                                // This is here to prevent the "empty" twin error from being thrown.
                                { "MoreStuff", "MoreStuffHereToo" }
                            }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);
            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout));
            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.Equal(10, deploymentConfigInfo.OrDefault().Version);
            Assert.Equal(deploymentConfigInfo.OrDefault().DeploymentConfig, deploymentConfig);
            Assert.NotNull(connectionStatusChangesHandler);
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncIncludesExceptionWhenDeserializeThrows()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(
                        JObject.FromObject(
                            new Dictionary<string, object>
                            {
                                { "$version", 10 },

                                // This is here to prevent the "empty" twin error from being thrown.
                                { "MoreStuff", "MoreStuffHereToo" }
                            }).ToString()),
                    Reported = new TwinCollection()
                }
            };

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Throws<FormatException>();

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout));

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);

            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType<ConfigFormatException>(deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncIncludesExceptionWhenDeserializeThrowsConfigEmptyException()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();

            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(
                        JObject.FromObject(
                            new Dictionary<string, object>
                            {
                                { "$version", 10 }
                            }).ToString()),
                    Reported = new TwinCollection()
                }
            };

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout));

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);

            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType<ConfigEmptyException>(deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoIncludesExceptionWhenSchemaVersionDoesNotMatch()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(
                        JObject.FromObject(
                            new Dictionary<string, object>
                            {
                                { "$version", 10 },

                                // This is here to prevent the "empty" twin error from being thrown.
                                { "MoreStuff", "MoreStuffHereToo" }
                            }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "InvalidSchemaVersion",
                runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>()))
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.SetMethodHandlerAsync(It.IsAny<string>(), It.IsAny<MethodCallback>()))
                .Returns(Task.CompletedTask);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout));

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType<InvalidSchemaVersionException>(deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncIDoesNotIncludeExceptionWhenGetTwinThrows()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            var retryStrategy = new Mock<RetryStrategy>(new object[] { false });
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;

            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ThrowsAsync(new InvalidOperationException());
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>()))
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.SetMethodHandlerAsync(It.IsAny<string>(), It.IsAny<MethodCallback>()))
                .Returns(Task.CompletedTask);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            retryStrategy.Setup(rs => rs.GetShouldRetry())
                .Returns(
                    (int retryCount, Exception lastException, out TimeSpan delay) =>
                    {
                        delay = TimeSpan.Zero;
                        return false;
                    });

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            IEdgeAgentConnection connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), true, TimeSpan.FromHours(1), retryStrategy.Object);

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            connectionStatusChangesHandler.Invoke(
                ConnectionStatus.Connected,
                ConnectionStatusChangeReason.Connection_Ok);
            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.False(deploymentConfigInfo.HasValue);
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncRetriesWhenGetTwinThrows()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            var retryStrategy = new Mock<RetryStrategy>(new object[] { false });
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;

            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            retryStrategy.SetupSequence(rs => rs.GetShouldRetry())
                .Returns(
                    (int retryCount, Exception lastException, out TimeSpan delay) =>
                    {
                        delay = TimeSpan.Zero;
                        return true;
                    });

            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(
                        JObject.FromObject(
                            new Dictionary<string, object>
                            {
                                { "$version", 10 },

                                // This is here to prevent the "empty" twin error from being thrown.
                                { "MoreStuff", "MoreStuffHereToo" }
                            }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            deviceClient.SetupSequence(d => d.GetTwinAsync())
                .ThrowsAsync(new InvalidOperationException())
                .ReturnsAsync(twin);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>()))
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.SetMethodHandlerAsync(It.IsAny<string>(), It.IsAny<MethodCallback>()))
                .Returns(Task.CompletedTask);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            IEdgeAgentConnection connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), true, TimeSpan.FromHours(1), retryStrategy.Object);

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.Equal(10, deploymentConfigInfo.OrDefault().Version);
            Assert.Equal(deploymentConfigInfo.OrDefault().DeploymentConfig, deploymentConfig);
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncReturnsConfigWhenThereAreNoErrorsWithPatch()
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback = null;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(
                        JObject.FromObject(
                            new Dictionary<string, object>
                            {
                                { "$version", 10 },

                                // This is here to prevent the "empty" twin error from being thrown.
                                { "MoreStuff", "MoreStuffHereToo" }
                            }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(
                    statusChanges => { connectionStatusChangesHandler = statusChanges; })
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>()))
                .Callback<DesiredPropertyUpdateCallback>(p => desiredPropertyUpdateCallback = p)
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.SetMethodHandlerAsync(It.IsAny<string>(), It.IsAny<MethodCallback>()))
                .Returns(Task.CompletedTask);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout));

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);

            // Act
            // now send a patch update
            var patch = new TwinCollection(
                JObject.FromObject(
                    new Dictionary<string, object>
                    {
                        { "$version", 11 }
                    }).ToString());
            await desiredPropertyUpdateCallback.Invoke(patch, null);

            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.Equal(11, deploymentConfigInfo.OrDefault().Version);
            Assert.Equal(deploymentConfigInfo.OrDefault().DeploymentConfig, deploymentConfig);
        }

        [Integration]
        [Fact]
        public async Task EdgeAgentConnectionStatusTest()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };

            try
            {
                edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

                await SetAgentDesiredProperties(registryManager, edgeDeviceId);

                string edgeAgentConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};ModuleId=$edgeAgent;SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
                IModuleClientProvider moduleClientProvider = new ModuleClientProvider(
                    edgeAgentConnectionString,
                    new SdkModuleClientProvider(),
                    Option.None<UpstreamProtocol>(),
                    Option.None<IWebProxy>(),
                    Option.None<string>(),
                    false,
                    TimeSpan.FromDays(1));

                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerDesiredModule) }
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeAgentDockerModule) }
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeHubDockerModule) }
                };

                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerRuntimeInfo) }
                };

                var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
                {
                    [typeof(IModule)] = moduleDeserializerTypes,
                    [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                    [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                    [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
                };

                ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);
                IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

                // Assert
                Module edgeAgentModule = await registryManager.GetModuleAsync(edgeDevice.Id, Constants.EdgeAgentModuleIdentityName);
                Assert.NotNull(edgeAgentModule);
                Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Disconnected);

                IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout));
                await Task.Delay(TimeSpan.FromSeconds(5));

                edgeAgentModule = await registryManager.GetModuleAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName);
                Assert.NotNull(edgeAgentModule);
                Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Connected);

                edgeAgentConnection.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(5));

                edgeAgentModule = await registryManager.GetModuleAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName);
                Assert.NotNull(edgeAgentModule);
                Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Disconnected);
            }
            finally
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(edgeDevice);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        [Integration]
        [Fact]
        public async Task EdgeAgentPingMethodTest()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };

            try
            {
                edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

                await SetAgentDesiredProperties(registryManager, edgeDeviceId);

                string edgeAgentConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};ModuleId=$edgeAgent;SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
                IModuleClientProvider moduleClientProvider = new ModuleClientProvider(
                    edgeAgentConnectionString,
                    new SdkModuleClientProvider(),
                    Option.None<UpstreamProtocol>(),
                    Option.None<IWebProxy>(),
                    Option.None<string>(),
                    false,
                    TimeSpan.FromDays(1));

                var moduleDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerDesiredModule) }
                };

                var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeAgentDockerModule) }
                };

                var edgeHubDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(EdgeHubDockerModule) }
                };

                var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                {
                    { DockerType, typeof(DockerRuntimeInfo) }
                };

                var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
                {
                    [typeof(IModule)] = moduleDeserializerTypes,
                    [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                    [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                    [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
                };

                ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);
                ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

                // Assert
                await Assert.ThrowsAsync<DeviceNotFoundException>(() => serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping")));

                IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout));
                await Task.Delay(TimeSpan.FromSeconds(10));

                CloudToDeviceMethodResult methodResult = await serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping"));
                Assert.NotNull(methodResult);
                Assert.Equal(200, methodResult.Status);

                CloudToDeviceMethodResult invalidMethodResult = await serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("poke"));
                Assert.NotNull(invalidMethodResult);
                Assert.Equal(400, invalidMethodResult.Status);

                edgeAgentConnection.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(5));

                await Assert.ThrowsAsync<DeviceNotFoundException>(() => serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping")));
            }
            finally
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(edgeDevice);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        [Fact]
        [Unit]
        public async Task EdgeAgentConnectionRefreshTest()
        {
            // Arrange
            var moduleDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(DockerDesiredModule) }
            };

            var edgeAgentDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(EdgeAgentDockerModule) }
            };

            var edgeHubDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(EdgeHubDockerModule) }
            };

            var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(DockerRuntimeInfo) }
            };

            var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = moduleDeserializerTypes,
                [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
            };

            ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);

            var runtimeInfo = new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.0", null));
            var edgeAgentDockerModule = new EdgeAgentDockerModule("docker", new DockerConfig("image", string.Empty), ImagePullPolicy.OnCreate, null, null);
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image", string.Empty),
                ImagePullPolicy.OnCreate,
                null,
                null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule),
                new Dictionary<string, IModule>());
            string deploymentConfigJson = serde.Serialize(deploymentConfig);
            var twin = new Twin(new TwinProperties { Desired = new TwinCollection(deploymentConfigJson) });

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.Setup(m => m.GetTwinAsync())
                .ReturnsAsync(twin);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .ReturnsAsync(moduleClient.Object);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };

            // Act
            using (var edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider.Object, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), true, TimeSpan.FromSeconds(3)))
            {
                await Task.Delay(TimeSpan.FromSeconds(8));

                // Assert
                moduleClient.Verify(m => m.GetTwinAsync(), Times.Exactly(3));
            }
        }

        [Fact]
        [Unit]
        public async Task GetTwinFailureDoesNotUpdateState()
        {
            // Arrange
            var moduleDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(DockerDesiredModule) }
            };

            var edgeAgentDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(EdgeAgentDockerModule) }
            };

            var edgeHubDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(EdgeHubDockerModule) }
            };

            var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(DockerRuntimeInfo) }
            };

            var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = moduleDeserializerTypes,
                [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
            };

            ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);

            var runtimeInfo = new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.0", null));
            var edgeAgentDockerModule = new EdgeAgentDockerModule("docker", new DockerConfig("image", string.Empty), ImagePullPolicy.OnCreate, null, null);
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image", string.Empty),
                ImagePullPolicy.OnCreate,
                null,
                null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule),
                new Dictionary<string, IModule>());
            string deploymentConfigJson = serde.Serialize(deploymentConfig);
            var twin = new Twin(new TwinProperties { Desired = new TwinCollection(deploymentConfigJson) });

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.Setup(m => m.GetTwinAsync())
                .ReturnsAsync(twin);
            moduleClient.SetupGet(m => m.IsActive).Returns(true);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .ReturnsAsync(moduleClient.Object);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler>();
            var retryStrategy = new FixedInterval(3, TimeSpan.FromSeconds(2));

            // Act
            using (var edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider.Object, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), true, TimeSpan.FromSeconds(10), retryStrategy))
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Option<DeploymentConfigInfo> receivedDeploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                // Assert
                Assert.True(receivedDeploymentConfigInfo.HasValue);
                Assert.False(receivedDeploymentConfigInfo.OrDefault().Exception.HasValue);
                Assert.Equal(deploymentConfig, receivedDeploymentConfigInfo.OrDefault().DeploymentConfig);

                moduleClient.Setup(m => m.GetTwinAsync())
                    .ThrowsAsync(new ObjectDisposedException("Dummy obj disp"));

                await Task.Delay(TimeSpan.FromSeconds(12));

                // Act
                receivedDeploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                // Assert
                moduleClient.Verify(m => m.GetTwinAsync(), Times.Exactly(5));
                Assert.True(receivedDeploymentConfigInfo.HasValue);
                Assert.False(receivedDeploymentConfigInfo.OrDefault().Exception.HasValue);
                Assert.Equal(deploymentConfig, receivedDeploymentConfigInfo.OrDefault().DeploymentConfig);
            }
        }

        [Fact]
        [Unit]
        public async Task GetTwinRetryLogicGetsNewClient()
        {
            // Arrange
            var moduleDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(DockerDesiredModule) }
            };

            var edgeAgentDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(EdgeAgentDockerModule) }
            };

            var edgeHubDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(EdgeHubDockerModule) }
            };

            var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
            {
                { DockerType, typeof(DockerRuntimeInfo) }
            };

            var deserializerTypes = new Dictionary<Type, IDictionary<string, Type>>
            {
                [typeof(IModule)] = moduleDeserializerTypes,
                [typeof(IEdgeAgentModule)] = edgeAgentDeserializerTypes,
                [typeof(IEdgeHubModule)] = edgeHubDeserializerTypes,
                [typeof(IRuntimeInfo)] = runtimeInfoDeserializerTypes,
            };

            ISerde<DeploymentConfig> serde = new TypeSpecificSerDe<DeploymentConfig>(deserializerTypes);

            var runtimeInfo = new DockerRuntimeInfo("docker", new DockerRuntimeConfig("1.0", null));
            var edgeAgentDockerModule = new EdgeAgentDockerModule("docker", new DockerConfig("image", string.Empty), ImagePullPolicy.OnCreate, null, null);
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image", string.Empty),
                ImagePullPolicy.OnCreate,
                null,
                null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule),
                new Dictionary<string, IModule>());
            string deploymentConfigJson = serde.Serialize(deploymentConfig);
            var twin = new Twin(new TwinProperties { Desired = new TwinCollection(deploymentConfigJson) });

            var edgeHubDockerModule2 = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image2", string.Empty),
                ImagePullPolicy.OnCreate,
                null,
                null);
            var deploymentConfig2 = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule2),
                new Dictionary<string, IModule>());
            string deploymentConfigJson2 = serde.Serialize(deploymentConfig2);
            var twin2 = new Twin(new TwinProperties { Desired = new TwinCollection(deploymentConfigJson2) });

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.Setup(m => m.GetTwinAsync())
                .ReturnsAsync(twin);
            moduleClient.SetupGet(m => m.IsActive).Returns(true);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .ReturnsAsync(moduleClient.Object);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler>();
            var retryStrategy = new FixedInterval(3, TimeSpan.FromSeconds(2));

            // Act
            using (var edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider.Object, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), true, TimeSpan.FromSeconds(10), retryStrategy))
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Option<DeploymentConfigInfo> receivedDeploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                // Assert
                Assert.True(receivedDeploymentConfigInfo.HasValue);
                Assert.False(receivedDeploymentConfigInfo.OrDefault().Exception.HasValue);
                Assert.Equal(deploymentConfig, receivedDeploymentConfigInfo.OrDefault().DeploymentConfig);
                Assert.NotEqual(deploymentConfig2, receivedDeploymentConfigInfo.OrDefault().DeploymentConfig);

                moduleClient.SetupSequence(m => m.GetTwinAsync())
                  .ThrowsAsync(new ObjectDisposedException("Dummy obj disp"))
                  .ThrowsAsync(new ObjectDisposedException("Dummy obj disp 2"))
                  .ReturnsAsync(twin2);

                await Task.Delay(TimeSpan.FromSeconds(12));

                // Assert
                moduleClient.Verify(m => m.GetTwinAsync(), Times.Exactly(4));

                // Act
                receivedDeploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                // Assert
                Assert.True(receivedDeploymentConfigInfo.HasValue);
                Assert.False(receivedDeploymentConfigInfo.OrDefault().Exception.HasValue);
                Assert.Equal(deploymentConfig2, receivedDeploymentConfigInfo.OrDefault().DeploymentConfig);
                Assert.NotEqual(deploymentConfig, receivedDeploymentConfigInfo.OrDefault().DeploymentConfig);
            }
        }

        [Theory]
        [Unit]
        [InlineData("1.0", null)]
        [InlineData("1.1", null)]
        [InlineData("1.2", null)]
        [InlineData("1.3", null)]
        [InlineData("1", typeof(InvalidSchemaVersionException))]
        [InlineData("", typeof(InvalidSchemaVersionException))]
        [InlineData(null, typeof(InvalidSchemaVersionException))]
        [InlineData("0.1", typeof(InvalidSchemaVersionException))]
        [InlineData("2.0", typeof(InvalidSchemaVersionException))]
        [InlineData("2.1", typeof(InvalidSchemaVersionException))]
        public void SchemaVersionCheckTest(string schemaVersion, Type expectedException)
        {
            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => EdgeAgentConnection.ValidateSchemaVersion(schemaVersion));
            }
            else
            {
                EdgeAgentConnection.ValidateSchemaVersion(schemaVersion);
            }
        }

        static async Task SetAgentDesiredProperties(RegistryManager rm, string deviceId)
        {
            var dp = new
            {
                schemaVersion = "1.0",
                runtime = new
                {
                    type = "docker",
                    settings = new
                    {
                        registryCredentials = new
                        {
                            r1 = new
                            {
                                address = "acr1.azure.net",
                                username = "u1",
                                password = "p1"
                            },
                            r2 = new
                            {
                                address = "acr2.azure.net",
                                username = "u2",
                                password = "p2"
                            }
                        }
                    }
                },
                systemModules = new
                {
                    edgeAgent = new
                    {
                        configuration = new
                        {
                            id = "1235"
                        },
                        type = "docker",
                        env = new
                        {
                            e1 = new
                            {
                                value = "e1val"
                            },
                            e2 = new
                            {
                                value = "e2val"
                            }
                        },
                        settings = new
                        {
                            image = "edgeAgent",
                            createOptions = string.Empty
                        }
                    },
                    edgeHub = new
                    {
                        type = "docker",
                        status = "running",
                        restartPolicy = "always",
                        env = new
                        {
                            e3 = new
                            {
                                value = "e3val"
                            },
                            e4 = new
                            {
                                value = "e4val"
                            }
                        },
                        settings = new
                        {
                            image = "edgeHub",
                            createOptions = string.Empty
                        }
                    }
                },
                modules = new
                {
                    mongoserver = new
                    {
                        version = "1.0",
                        type = "docker",
                        status = "running",
                        restartPolicy = "on-failure",
                        env = new
                        {
                            e5 = new
                            {
                                value = "e5val"
                            },
                            e6 = new
                            {
                                value = "e6val"
                            }
                        },
                        settings = new
                        {
                            image = "mongo",
                            createOptions = string.Empty
                        }
                    }
                }
            };
            var cc = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = dp
                    }
                }
            };
            await rm.ApplyConfigurationContentOnDeviceAsync(deviceId, cc);
        }

        static async Task DeleteConfigurationAsync(RegistryManager registryManager, string configurationId)
        {
            await registryManager.RemoveConfigurationAsync(configurationId);
        }

        static async Task UpdateAgentDesiredProperties(RegistryManager rm, string deviceId)
        {
            var dp = new
            {
                schemaVersion = "1.0",
                runtime = new
                {
                    type = "docker",
                    settings = new
                    {
                        registryCredentials = new
                        {
                            r1 = new
                            {
                                address = "acr1.azure.net",
                                username = "u1",
                                password = "p1"
                            },
                            r2 = new
                            {
                                address = "acr2.azure.net",
                                username = "u2",
                                password = "p2"
                            }
                        }
                    }
                },
                systemModules = new
                {
                    edgeAgent = new
                    {
                        configuration = new
                        {
                            id = "1235"
                        },
                        type = "docker",
                        settings = new
                        {
                            image = "edgeAgent",
                            createOptions = string.Empty
                        }
                    },
                    edgeHub = new
                    {
                        type = "docker",
                        status = "running",
                        restartPolicy = "always",
                        settings = new
                        {
                            image = "edgeHub",
                            createOptions = string.Empty
                        }
                    }
                },
                modules = new
                {
                    mongoserver = new
                    {
                        version = "1.0",
                        type = "docker",
                        status = "running",
                        restartPolicy = "on-failure",
                        env = new
                        {
                            e5 = new
                            {
                                value = "e5val"
                            },
                            e7 = new
                            {
                                value = "e7val"
                            }
                        },
                        settings = new
                        {
                            image = "mongo",
                            createOptions = string.Empty
                        }
                    },
                    mlModule = new
                    {
                        version = "1.0",
                        type = "docker",
                        status = "running",
                        restartPolicy = "on-unhealthy",
                        settings = new
                        {
                            image = "ml:latest",
                            createOptions = string.Empty
                        }
                    }
                }
            };

            var cc = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = dp
                    }
                }
            };

            await rm.ApplyConfigurationContentOnDeviceAsync(deviceId, cc);
        }

        static void ValidateModules(DeploymentConfig deploymentConfig)
        {
            Assert.True(deploymentConfig.SystemModules.EdgeAgent.HasValue);
            Assert.True(deploymentConfig.SystemModules.EdgeHub.HasValue);

            var edgeAgent = deploymentConfig.SystemModules.EdgeAgent.OrDefault() as EdgeAgentDockerModule;
            Assert.NotNull(edgeAgent);
            Assert.Equal("e1val", edgeAgent.Env["e1"].Value);
            Assert.Equal("e2val", edgeAgent.Env["e2"].Value);

            var edgeHub = deploymentConfig.SystemModules.EdgeHub.OrDefault() as EdgeHubDockerModule;
            Assert.NotNull(edgeHub);
            Assert.Equal("e3val", edgeHub.Env["e3"].Value);
            Assert.Equal("e4val", edgeHub.Env["e4"].Value);

            var module1 = deploymentConfig.Modules["mongoserver"] as DockerDesiredModule;
            Assert.NotNull(module1);
            Assert.Equal("e5val", module1.Env["e5"].Value);
            Assert.Equal("e6val", module1.Env["e6"].Value);
        }

        static void ValidateRuntimeConfig(IRuntimeInfo deploymentConfigRuntime)
        {
            var dockerRuntimeConfig = deploymentConfigRuntime as IRuntimeInfo<DockerRuntimeConfig>;
            Assert.NotNull(dockerRuntimeConfig);

            Assert.Null(dockerRuntimeConfig.Config.LoggingOptions);
            Assert.Equal(2, dockerRuntimeConfig.Config.RegistryCredentials.Count);
            RegistryCredentials r1 = dockerRuntimeConfig.Config.RegistryCredentials["r1"];
            Assert.Equal("acr1.azure.net", r1.Address);
            Assert.Equal("u1", r1.Username);
            Assert.Equal("p1", r1.Password);

            RegistryCredentials r2 = dockerRuntimeConfig.Config.RegistryCredentials["r2"];
            Assert.Equal("acr2.azure.net", r2.Address);
            Assert.Equal("u2", r2.Username);
            Assert.Equal("p2", r2.Password);
        }

        static object GetEdgeAgentConfiguration()
        {
            var desiredProperties = new
            {
                schemaVersion = "1.0",
                runtime = new
                {
                    type = "docker",
                    settings = new
                    {
                        loggingOptions = string.Empty
                    }
                },
                systemModules = new
                {
                    edgeAgent = new
                    {
                        type = "docker",
                        settings = new
                        {
                            image = "edgeAgent",
                            createOptions = string.Empty
                        }
                    },
                    edgeHub = new
                    {
                        type = "docker",
                        status = "running",
                        restartPolicy = "always",
                        settings = new
                        {
                            image = "edgeHub",
                            createOptions = string.Empty
                        }
                    }
                },
                modules = new
                {
                    mongoserver = new
                    {
                        version = "1.0",
                        type = "docker",
                        status = "running",
                        restartPolicy = "on-failure",
                        settings = new
                        {
                            image = "mongo",
                            createOptions = string.Empty
                        }
                    },
                    asa = new
                    {
                        version = "1.0",
                        type = "docker",
                        status = "running",
                        restartPolicy = "on-failure",
                        settings = new
                        {
                            image = "asa",
                            createOptions = string.Empty
                        }
                    }
                }
            };
            return desiredProperties;
        }

        static object GetEdgeHubConfiguration()
        {
            var desiredProperties = new
            {
                schemaVersion = "1.0",
                routes = new Dictionary<string, string>
                {
                    ["route1"] = "from /* INTO $upstream",
                },
                storeAndForwardConfiguration = new
                {
                    timeToLiveSecs = 20
                }
            };
            return desiredProperties;
        }

        static object GetTwinConfiguration(string moduleName)
        {
            var desiredProperties = new
            {
                name = moduleName
            };
            return desiredProperties;
        }
    }
}
