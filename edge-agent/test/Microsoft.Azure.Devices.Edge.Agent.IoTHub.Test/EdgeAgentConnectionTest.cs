// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test;
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

        static async Task CreateConfigurationAsync(RegistryManager registryManager, string configurationId, string targetCondition, int priority)
        {
            var configuration = new Configuration(configurationId)
            {
                Labels = new Dictionary<string, string>
                {
                    { "App", "Stream Analytics" }
                },
                Content = GetCombinedConfigurationContent(),
                Priority = priority,
                TargetCondition = targetCondition
            };

            await registryManager.AddConfigurationAsync(configuration);
        }

        static async Task CreateBaseAddOnConfigurationsAsync(RegistryManager registryManager, string configurationId, string addOnConfigurationId, string targetCondition, int priority)
        {
            var configuration = new Configuration(configurationId)
            {
                Labels = new Dictionary<string, string>
                {
                    { "App", "Mongo" }
                },
                Content = GetBaseConfigurationContent(),
                Priority = priority,
                TargetCondition = targetCondition
            };

            var addonConfiguration = new Configuration(addOnConfigurationId)
            {
                Labels = new Dictionary<string, string>
                {
                    { "Addon", "Stream Analytics" }
                },
                Content = GetAddOnConfigurationContent(),
                Priority = priority + 1,
                TargetCondition = targetCondition
            };

            await registryManager.AddConfigurationAsync(configuration);
            await registryManager.AddConfigurationAsync(addonConfiguration);
        }

        static TwinCollection GetEdgeAgentReportedProperties(DeploymentConfigInfo deploymentConfigInfo)
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

        static ConfigurationContent GetCombinedConfigurationContent() =>
            new ConfigurationContent
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

        static ConfigurationContent GetBaseConfigurationContent() =>
            new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetEdgeAgentBaseConfiguration()
                    },
                    ["$edgeHub"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetEdgeHubConfiguration()
                    },
                    ["mongoserver"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetTwinConfiguration("mongoserver")
                    }
                }
            };

        static ConfigurationContent GetAddOnConfigurationContent() =>
            new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired.modules.asa"] = GetEdgeAgentAddOnConfiguration()
                    },
                    ["asa"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = GetTwinConfiguration("asa")
                    },
                    ["$edgeHub"] = new Dictionary<string, object>
                    {
                        ["properties.desired.routes.route1"] = "from /* INTO $upstream"
                    }
                }
            };

        static IEdgeAgentConnection CreateEdgeAgentConnection(IotHubConnectionStringBuilder iotHubConnectionStringBuilder, string edgeDeviceId, Device edgeDevice)
        {
            string edgeAgentConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};ModuleId=$edgeAgent;SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
            IModuleClientProvider moduleClientProvider = new ModuleClientProvider(
                edgeAgentConnectionString,
                new SdkModuleClientProvider(),
                Option.None<UpstreamProtocol>(),
                Option.None<IWebProxy>(),
                Constants.IoTEdgeAgentProductInfoIdentifier,
                false,
                TimeSpan.FromDays(1),
                false);

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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();
            IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);
            return edgeAgentConnection;
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

                var edgeAgentConnection = CreateEdgeAgentConnection(iotHubConnectionStringBuilder, edgeDeviceId, edgeDevice);

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

        static async Task<Device> CreateEdgeDeviceWithCondition(string edgeDeviceId, RegistryManager registryManager, string conditionPropertyName, string conditionPropertyValue)
        {
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
            return edgeDevice;
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

                var edgeDevice = await CreateEdgeDeviceWithCondition(edgeDeviceId, registryManager, conditionPropertyName, conditionPropertyValue);

                await CreateConfigurationAsync(registryManager, configurationId, $"tags.{conditionPropertyName}='{conditionPropertyValue}'", 10);

                // Service takes about 5 mins to sync config to twin
                await Task.Delay(TimeSpan.FromMinutes(7));

                var edgeAgentConnection = CreateEdgeAgentConnection(iotHubConnectionStringBuilder, edgeDeviceId, edgeDevice);

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

        [Integration]
        [Fact]
        public async Task EdgeAgentConnectionBaseAddOnConfigurationTest()
        {
            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid();
            string configurationId = "testconfiguration-" + Guid.NewGuid().ToString();
            string addOnConfigurationId = "addon" + configurationId;
            string conditionPropertyName = "condition-" + Guid.NewGuid().ToString("N");
            string conditionPropertyValue = Guid.NewGuid().ToString();
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            try
            {
                await registryManager.OpenAsync();

                var edgeDevice = await CreateEdgeDeviceWithCondition(edgeDeviceId, registryManager, conditionPropertyName, conditionPropertyValue);

                await CreateBaseAddOnConfigurationsAsync(registryManager, configurationId, addOnConfigurationId, $"tags.{conditionPropertyName}='{conditionPropertyValue}'", 10);

                // Service takes about 5 mins to sync config to twin
                await Task.Delay(TimeSpan.FromMinutes(7));

                var edgeAgentConnection = CreateEdgeAgentConnection(iotHubConnectionStringBuilder, edgeDeviceId, edgeDevice);

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

                Configuration addOnConfig = await registryManager.GetConfigurationAsync(addOnConfigurationId);
                Assert.NotNull(addOnConfig);
                Assert.NotNull(addOnConfig.SystemMetrics);
                Assert.True(addOnConfig.SystemMetrics.Results.ContainsKey("targetedCount"));
                Assert.Equal(1, addOnConfig.SystemMetrics.Results["targetedCount"]);
                Assert.True(addOnConfig.SystemMetrics.Results.ContainsKey("appliedCount"));
                Assert.Equal(1, addOnConfig.SystemMetrics.Results["appliedCount"]);
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
                    await DeleteConfigurationAsync(registryManager, addOnConfigurationId);
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
                ImmutableDictionary<string, IModule>.Empty,
                null);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(d => d.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(statusChanges => connectionStatusChangesHandler = statusChanges)
                .ReturnsAsync(deviceClient.Object);

            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);
            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);
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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

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

            // The ReprovisionDeviceAsync API should not be called.
            deviceManager.Verify(x => x.ReprovisionDeviceAsync(), Times.Never);
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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

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

            // The ReprovisionDeviceAsync API should not be called.
            deviceManager.Verify(x => x.ReprovisionDeviceAsync(), Times.Never);
        }

        [Theory]
        [Unit]
        [InlineData(UpstreamProtocol.Amqp, ConnectionStatusChangeReason.Device_Disabled, true)]
        [InlineData(UpstreamProtocol.AmqpWs, ConnectionStatusChangeReason.Device_Disabled, true)]
        [InlineData(UpstreamProtocol.Mqtt, ConnectionStatusChangeReason.Bad_Credential, true)]
        [InlineData(UpstreamProtocol.MqttWs, ConnectionStatusChangeReason.Bad_Credential, true)]
        [InlineData(UpstreamProtocol.Amqp, ConnectionStatusChangeReason.Bad_Credential, true)]
        [InlineData(UpstreamProtocol.AmqpWs, ConnectionStatusChangeReason.Bad_Credential, true)]
        [InlineData(UpstreamProtocol.Amqp, ConnectionStatusChangeReason.Communication_Error, false)]
        [InlineData(UpstreamProtocol.AmqpWs, ConnectionStatusChangeReason.Communication_Error, false)]
        [InlineData(UpstreamProtocol.Mqtt, ConnectionStatusChangeReason.Communication_Error, false)]
        [InlineData(UpstreamProtocol.MqttWs, ConnectionStatusChangeReason.Communication_Error, false)]
        internal async Task ConnectionStatusChangeReasonReprovisionsDevice(
            UpstreamProtocol protocol, ConnectionStatusChangeReason connectionStatusChangeReason, bool shouldReprovision)
        {
            // Arrange
            var deviceClient = new Mock<IModuleClient>();
            deviceClient.Setup(x => x.UpstreamProtocol).Returns(protocol);
            deviceClient.Setup(x => x.IsActive).Returns(true);
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

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
            var deviceManager = new Mock<IDeviceManager>();
            deviceManager.Setup(x => x.ReprovisionDeviceAsync()).Returns(Task.CompletedTask);
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.NotNull(connectionStatusChangesHandler);

            // Act
            connectionStatusChangesHandler.Invoke(ConnectionStatus.Connected, connectionStatusChangeReason);

            // Assert
            // Whether the ReprovisionDeviceAsync API has been called based on the appropriate protocol and connection status change reason.
            deviceManager.Verify(x => x.ReprovisionDeviceAsync(), Times.Exactly(shouldReprovision ? 1 : 0));
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
                ImmutableDictionary<string, IModule>.Empty,
                null);

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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

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

            // The ReprovisionDeviceAsync API should not be called.
            deviceManager.Verify(x => x.ReprovisionDeviceAsync(), Times.Never);
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
                ImmutableDictionary<string, IModule>.Empty,
                null);

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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            IEdgeAgentConnection connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, true, TimeSpan.FromHours(1), retryStrategy.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

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

            // The ReprovisionDeviceAsync API should not be called.
            deviceManager.Verify(x => x.ReprovisionDeviceAsync(), Times.Never);
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
                ImmutableDictionary<string, IModule>.Empty,
                null);

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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            IEdgeAgentConnection connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, true, TimeSpan.FromHours(1), retryStrategy.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

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
                ImmutableDictionary<string, IModule>.Empty,
                null);

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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();
            var connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

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

            // The ReprovisionDeviceAsync API should not be called.
            deviceManager.Verify(x => x.ReprovisionDeviceAsync(), Times.Never);
        }

        // Connection state is only updated for telemetry path. Since EdgeAgent doesn’t
        // send any telemetry, it will show as not connected. It is a long standing PBI
        // for hub to update connection state for twin operations, but has never been
        // prioritized.
        [Integration]
        [Fact(Skip = "Disabled to unblock CI pipeline.")]
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
                    Constants.IoTEdgeAgentProductInfoIdentifier,
                    false,
                    TimeSpan.FromDays(1),
                    false);

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
                var deviceManager = new Mock<IDeviceManager>();
                IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

                // Assert
                Module edgeAgentModule = await registryManager.GetModuleAsync(edgeDevice.Id, Constants.EdgeAgentModuleIdentityName);
                Assert.NotNull(edgeAgentModule);
                Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Disconnected);

                IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);
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
                    Constants.IoTEdgeAgentProductInfoIdentifier,
                    false,
                    TimeSpan.FromDays(1),
                    false);

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
                var deviceManager = new Mock<IDeviceManager>();
                IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

                // Assert
                await Assert.ThrowsAsync<DeviceNotFoundException>(() => serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping")));

                IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);
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
            var edgeAgentDockerModule = new EdgeAgentDockerModule("docker", new DockerConfig("image", string.Empty, Option.None<string>()), ImagePullPolicy.OnCreate, null, null);
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image", string.Empty, Option.None<string>()),
                ImagePullPolicy.OnCreate,
                Constants.DefaultStartupOrder,
                null,
                null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule),
                new Dictionary<string, IModule>(),
                null);
            string deploymentConfigJson = serde.Serialize(deploymentConfig);
            var twin = new Twin(new TwinProperties { Desired = new TwinCollection(deploymentConfigJson) });

            var moduleClient = new Mock<IModuleClient>();
            moduleClient.Setup(m => m.GetTwinAsync())
                .ReturnsAsync(twin);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(m => m.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .ReturnsAsync(moduleClient.Object);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            using (var edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider.Object, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, true, TimeSpan.FromSeconds(3), Mock.Of<IDeploymentMetrics>(), manifestTrustBundle))
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
            var edgeAgentDockerModule = new EdgeAgentDockerModule("docker", new DockerConfig("image", string.Empty, Option.None<string>()), ImagePullPolicy.OnCreate, null, null);
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image", string.Empty, Option.None<string>()),
                ImagePullPolicy.OnCreate,
                Constants.DefaultStartupOrder,
                null,
                null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule),
                new Dictionary<string, IModule>(),
                null);
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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            using (var edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider.Object, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, true, TimeSpan.FromSeconds(10), retryStrategy, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle))
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
                moduleClient.Verify(m => m.GetTwinAsync(), Times.Exactly(7));
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
            var edgeAgentDockerModule = new EdgeAgentDockerModule("docker", new DockerConfig("image", string.Empty, Option.None<string>()), ImagePullPolicy.OnCreate, null, null);
            var edgeHubDockerModule = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image", string.Empty, Option.None<string>()),
                ImagePullPolicy.OnCreate,
                Constants.DefaultStartupOrder,
                null,
                null);
            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule),
                new Dictionary<string, IModule>(),
                null);
            string deploymentConfigJson = serde.Serialize(deploymentConfig);
            var twin = new Twin(new TwinProperties { Desired = new TwinCollection(deploymentConfigJson) });

            var edgeHubDockerModule2 = new EdgeHubDockerModule(
                "docker",
                ModuleStatus.Running,
                RestartPolicy.Always,
                new DockerConfig("image2", string.Empty, Option.None<string>()),
                ImagePullPolicy.OnCreate,
                Constants.DefaultStartupOrder,
                null,
                null);
            var deploymentConfig2 = new DeploymentConfig(
                "1.0",
                runtimeInfo,
                new SystemModules(edgeAgentDockerModule, edgeHubDockerModule2),
                new Dictionary<string, IModule>(),
                null);
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
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            using (var edgeAgentConnection = new EdgeAgentConnection(moduleClientProvider.Object, serde, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, true, TimeSpan.FromSeconds(10), retryStrategy, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle))
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

        [Unit]
        [Theory]
        [MemberData(nameof(GetTwinCollectionForTwinSignature))]
        public void TestCheckTwinSignatureIsValid(bool expectedResult, TwinCollection twinDesiredProperties)
        {
            Assert.Equal(expectedResult, EdgeAgentConnection.CheckIfTwinSignatureIsValid(twinDesiredProperties));
        }

        public static IEnumerable<object[]> GetTwinCollectionForTwinSignature()
        {
            var integrityWithEcdsaCerts = new
            {
                header = new
                {
                    signercert = GetEcdsaSignerTestCert(),
                    intermediatecacert = GetEcdsaIntermediateCATestCert(),
                },
                signature = new
                {
                    bytes = GetEcdsaTestEdgeAgentSignature(),
                    algorithm = "ES256"
                }
            };

            var integrityWithRsaCerts = new
            {
                header = new
                {
                    signercert = GetRsaSignerTestCert(),
                    intermediatecacert = GetRsaIntermediateCATestCert(),
                },
                signature = new
                {
                    bytes = GetRsaTestEdgeAgentSignature(),
                    algorithm = "RS256"
                }
            };
            string edgeAgentRightImageName = "mcr.microsoft.com/azureiotedge-agent:1.0";
            string edgeAgentWrongImageName = "mcr.microsoft.com/azureiotedge-wrong-agent:1.0";

            yield return new object[] { true, GetTwinDesiredProperties(edgeAgentRightImageName, integrityWithEcdsaCerts) };
            yield return new object[] { false, GetTwinDesiredProperties(edgeAgentWrongImageName, integrityWithEcdsaCerts) };
            yield return new object[] { true, GetTwinDesiredProperties(edgeAgentRightImageName, integrityWithRsaCerts) };
            yield return new object[] { false, GetTwinDesiredProperties(edgeAgentWrongImageName, integrityWithRsaCerts) };
        }

        [Unit]
        [Theory]
        [MemberData(nameof(GetTwinCollectionForIntegrityCheck))]
        public void TestCheckIfTwinPropertiesAreSigned(bool expectedResult, TwinCollection twinDesiredProperties)
        {
            Assert.Equal(expectedResult, EdgeAgentConnection.CheckIfTwinPropertiesAreSigned(twinDesiredProperties));
        }

        public static IEnumerable<object[]> GetTwinCollectionForIntegrityCheck()
        {
            var integrity = new
            {
                header = new
                {
                    signercert = new string[] { "signercert1", "signercert2" },
                    intermediatecacert = new string[] { "intermediatecacert1", "intermediatecacert2" }
                },
                signature = new
                {
                    bytes = "signature",
                    algorithm = "algo"
                }
            };
            string edgeAgentImageName = "mcr.microsoft.com/azureiotedge-agent:1.0";

            TwinCollection twinDesiredPropertiesWithoutIntegrity = GetTwinDesiredProperties(edgeAgentImageName, null);
            TwinCollection twinDesiredPropertiesWithIntegrity = GetTwinDesiredProperties(edgeAgentImageName, integrity);
            yield return new object[] { true, twinDesiredPropertiesWithIntegrity };
            yield return new object[] { false, twinDesiredPropertiesWithoutIntegrity };
        }

        static TwinCollection GetTwinDesiredProperties(string edgeAgentImageName, object integrity)
        {
            var desiredProperties = new
            {
                modules = new { },
                runtime = new
                {
                    settings = new
                    {
                        minDockerVersion = "v1.25"
                    },
                    type = "docker"
                },
                schemaVersion = "1.1",
                systemModules = new
                {
                    edgeAgent = new
                    {
                        settings = new
                        {
                            image = edgeAgentImageName,
                            createOptions = string.Empty,
                        },
                        type = "docker"
                    },
                    edgeHub = new
                    {
                        settings = new
                        {
                            image = "mcr.microsoft.com/azureiotedge-hub:1.0",
                            createOptions = "{\"HostConfig\":{\"PortBindings\":{\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}],\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}",
                        },
                        type = "docker",
                        status = "running",
                        restartPolicy = "always"
                    }
                },
                integrity,
                version = "10"
            };

            return new TwinCollection(JsonConvert.SerializeObject(desiredProperties));
        }

        [Unit]
        [Theory]
        [MemberData(nameof(GetTwinForVerifyingSignatures))]
        public void TestExtractAgentTwinAndVerify(string type, string imageName, string algo, string[] signercert, string[] intermediatecacert, string signature)
        {
            var desiredProperties = new
            {
                modules = new { },
                runtime = new
                {
                    settings = new
                    {
                        minDockerVersion = "v1.25"
                    },
                    type = "docker"
                },
                schemaVersion = "1.1",
                systemModules = new
                {
                    edgeAgent = new
                    {
                        settings = new
                        {
                            image = imageName,
                            createOptions = string.Empty,
                        },
                        type = "docker"
                    },
                    edgeHub = new
                    {
                        settings = new
                        {
                            image = "mcr.microsoft.com/azureiotedge-hub:1.0",
                            createOptions = "{\"HostConfig\":{\"PortBindings\":{\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}],\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}",
                        },
                        type = "docker",
                        status = "running",
                        restartPolicy = "always"
                    }
                },
                integrity = new
                {
                    header = new
                    {
                        signercert,
                        intermediatecacert,
                    },
                    signature = new
                    {
                        bytes = signature,
                        algorithm = algo
                    }
                },
                version = "10"
            };

            TwinCollection twinDesiredProperties = new TwinCollection(JsonConvert.SerializeObject(desiredProperties));
            if (type == "good")
            {
                Assert.True(EdgeAgentConnection.ExtractAgentTwinAndVerify(twinDesiredProperties));
            }
            else
            {
                Assert.False(EdgeAgentConnection.ExtractAgentTwinAndVerify(twinDesiredProperties));
            }
        }

        public static IEnumerable<object[]> GetTwinForVerifyingSignatures()
        {
            string goodImageName = "mcr.microsoft.com/azureiotedge-agent:1.0";
            string maliciousImageName = "mcr.microsoft.com/azureiotedge-agent-malicious-image:1.0";

            string[] ecdsaSignerCert = GetEcdsaSignerTestCert();
            string[] ecdsaIntermediateCACert = GetEcdsaIntermediateCATestCert();
            string ecdsaSignature = GetEcdsaTestEdgeAgentSignature();
            string ecdsaAlgo = "ES256";

            string[] rsaSignerCert = GetRsaSignerTestCert();
            string[] rsaIntermediateCACert = GetRsaIntermediateCATestCert();
            string rsaSignature = GetRsaTestEdgeAgentSignature();
            string rsaAlgo = "RS256";

            yield return new object[] { "good", goodImageName, ecdsaAlgo, ecdsaSignerCert, ecdsaIntermediateCACert, ecdsaSignature };
            yield return new object[] { "good", goodImageName, rsaAlgo, rsaSignerCert, rsaIntermediateCACert, rsaSignature };
            yield return new object[] { "bad", maliciousImageName, rsaAlgo, rsaSignerCert, rsaIntermediateCACert, rsaSignature };
            yield return new object[] { "bad", maliciousImageName, ecdsaAlgo, ecdsaSignerCert, ecdsaIntermediateCACert, ecdsaSignature };
        }

        public static string[] GetEcdsaSignerTestCert() => new string[]
            {
                "\rMIICOTCCAd+gAwIBAgICEAAwCgYIKoZIzj0EAwIwVDELMAkGA1UEAwwCc3MxCzAJ\rBgNVBAgMAldBMQswCQYDVQQGEwJVUzERMA8GCSqGSIb3DQEJARYCc3MxCzAJBgNV\rBAoMAnNzMQswCQYDVQQLDAJzczAeFw0xNTA1MDUwMDAwMDBaFw0yNDEyMzEyMzU5\rNTlaMFQxCzAJBgNVBAMMAnNzMQswCQYDVQQIDAJXQTELMAkGA1UEBhMCVVMxETAP\rBgkqhkiG9w0BCQEWAnNzMQswCQYDVQQKDAJzczELMAkGA1UECwwCc3MwWTATBgcq\rhkjOPQIBBggqhkjOPQMBBwNCAAS7kA6viM5eN1Y/E+1KUOjLEZdhsygtbntGqV",
                "7s\rMXG5ZEKr+drie2i6lMa8zu/hvHhOdbXiFVOZT045AYaGWBDRo4GgMIGdMAwGA1Ud\rEwEB/wQCMAAwHQYDVR0OBBYEFK0CsUii+1a5RlE+2aQMKrxwlFkeMB8GA1UdIwQY\rMBaAFI3svRm8zDySNcXiJCaqn6phhFtPMAsGA1UdDwQEAwIBpjATBgNVHSUEDDAK\rBggrBgEFBQcDATArBgNVHREEJDAigg9hLmEuZXhhbXBsZS5jb22CD2IuYi5leGFt\rcGxlLmNvbTAKBggqhkjOPQQDAgNIADBFAiBWXuB2+R1lXV3HPmmu7eJc3H2rpr8o\rKwR8wnDdnuYL+AIhAIM5nw1LLtEVKpIOP7DsrlxEQjPw1+nrj4/Ilb47Bqpq\r"
            };

        public static string[] GetEcdsaIntermediateCATestCert() => new string[]
            {
                "\rMIICRTCCAeugAwIBAgICEAAwCgYIKoZIzj0EAwIwYTELMAkGA1UEBhMCVVMxCzAJ\rBgNVBAgMAldBMQswCQYDVQQHDAJzczELMAkGA1UECgwCc3MxCzAJBgNVBAsMAnNz\rMQswCQYDVQQDDAJzczERMA8GCSqGSIb3DQEJARYCc3MwHhcNMTUwNTA1MDAwMDAw\rWhcNMjQxMjMxMjM1OTU5WjBUMQswCQYDVQQDDAJzczELMAkGA1UECAwCV0ExCzAJ\rBgNVBAYTAlVTMREwDwYJKoZIhvcNAQkBFgJzczELMAkGA1UECgwCc3MxCzAJBgNV\rBAsMAnNzMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE1Di5/tzZFOG1KrfoPwBa\rfgjF9I",
                "DWI7EL5DIeowGfr/MyUmtwULyrLE2bAQUGv9KdH2oPg6aK//WutYqli6MN\rXaOBnzCBnDAPBgNVHRMBAf8EBTADAQH/MB0GA1UdDgQWBBSN7L0ZvMw8kjXF4iQm\rqp+qYYRbTzAfBgNVHSMEGDAWgBTNmen1wYUOUouvXOzNt+4Gk2ox1zALBgNVHQ8E\rBAMCAaYwEwYDVR0lBAwwCgYIKwYBBQUHAwEwJwYDVR0RBCAwHoINYS5leGFtcGxl\rLmNvbYINYi5leGFtcGxlLmNvbTAKBggqhkjOPQQDAgNIADBFAiBETS1txVUaZl8E\rWagr5+OFGbHEluKTVD3hltzIjnJ+eAIhAIJbxmhIItZyEYpK6Pwy8eIWWO0u9Eu9\rg4oUYwl08mbk\r"
            };

        public static string GetEcdsaTestEdgeAgentSignature() => "c9AvfvMrcgv+jgamtd2VcHJbkajpu+7FfweuCt1ld7x4eDkGLDPuNNmEQkM1cZGwW1JXUd/zHLRp9rs1x9Zg3A==";

        public static string[] GetRsaSignerTestCert() => new string[]
            {
                "\rMIIFxTCCA62gAwIBAgICEAAwDQYJKoZIhvcNAQELBQAwVDELMAkGA1UEAwwCc3Mx\rCzAJBgNVBAgMAldBMQswCQYDVQQGEwJVUzERMA8GCSqGSIb3DQEJARYCc3MxCzAJ\rBgNVBAoMAnNzMQswCQYDVQQLDAJzczAeFw0xNTA1MDUwMDAwMDBaFw0yNDEyMzEy\rMzU5NTlaMFQxCzAJBgNVBAMMAnNzMQswCQYDVQQIDAJXQTELMAkGA1UEBhMCVVMx\rETAPBgkqhkiG9w0BCQEWAnNzMQswCQYDVQQKDAJzczELMAkGA1UECwwCc3MwggIi\rMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQC/kWLjEMiQnAc7c3JP8PEIEA58\rmIkdQwaRNWhmZW0OsifBHJJvFgyA5CGEazQaFhcrnMjF2T8/eCoIg7/9+6bsX322\rYLLphXW87aohvUYF0VCcomQRuaYHSxKEGXyAoxmvoywuQP3CELnb6PKM71jLYZn4\r+6kvDHcfZ9vY+jlH4sZRc36sauBwxf3voqt4/07PcHKy6WiPElOd+jZsf82lDTp2\rSLad/cZI8fxJqJth7t9g2b99vEUOtSbs9OliAIwTVAHMIWjsP/dbvNe39TlkrRf3\r7XIWD0RoS6apvE/CFfr4gHFJBxYB553y7KOpdURocnTTQNoMmAqm7bZpVUril48/\r7HBx4qaMz+/h7Vbdn+xhIJmHwGAaylzB9p5lpHdAQ/aSYSSwqkqKh0+hCD8wA5Zt\rcpoSBS+rxGgNtWJrAEjMuatIIu055ckf8lqyD7I8AVSUVuZ5IzumVwgMdRdN3L86\rM9JtknGnGIwFeb4l3S/NCxzhTZmgSY1aZ6uXiAJrvjx5i5J8gx8Mw5OCUGsKks/v\redMV3JFUJiJoleDxu7RQMF5Dy2XlKe26/QiM4DdRyCv7GvDO6oMv9Gudl7FEnt/a\roxibOWppmgEI5fHyPwZdiMPpu7qL",
                "+6AbkFAOQjyTh3Ri5Om9YaXV94zeTrL36ZsR\rA/s29xO/pB437azkcwIDAQABo4GgMIGdMAwGA1UdEwEB/wQCMAAwHQYDVR0OBBYE\rFHoCPM2gglKW8RF8+O/ao3wZMlZsMB8GA1UdIwQYMBaAFLVFJPuERtKpzqIC6fLQ\rZncCpGFaMAsGA1UdDwQEAwIBpjATBgNVHSUEDDAKBggrBgEFBQcDATArBgNVHREE\rJDAigg9hLmEuZXhhbXBsZS5jb22CD2IuYi5leGFtcGxlLmNvbTANBgkqhkiG9w0B\rAQsFAAOCAgEAEiLZpeycdnGWwNUS8LqcLVuJgAJEe10XgKWeUVHBSx3z1thzliXP\rbVsxiiW9V+3lNSaJJSbQkJVPDLV/sKdmSp3dQ/ll3abeSnLzap5FCco1wm2Ru8Vb\rNdJrRLW2hyQ+TFUrWGr9PqK5q6qKuQUywidFZkSvpLOL1eW5jTqUli1GzZio7YD1\rF/Qd4RBbKQTHbtrUMLwJujKIkAh/9dG13WevtdysxaLOYCmckytbE5Af+m1SSERa\r9FjUu22FAwIm9hk64NQgDlML6JKBj03rts51q+FO+D6U6c/VR3rmtZpvqy/Okf4X\rO82+SiTQ1EHLQpIKJhdAfJ7tpDMu0Hz3+qjRX1B8gpK4rnUoPMmmy4cTGjbDzKlL\rcJIP66xbR4tM0O8v1eWu4fJgHlPuYjok/tiIAxZFs8SKomeIEJSNaiOawp5XOshR\r3dggXZey6TDBB4uO2jgGNBQwu4vrFYlZVCcivPbKutjNHB3uhiBrA0yyeD/df1yX\rqwzlSg7cay0WMAbddK0jCFmrXbyRyAuoP/HB1UdQ7LygjsvPdf626xd9w6PihgxD\r9i0AeEqTVdwWfPpiDtRxGhJv/Kz9k17dVFYnJG7oMJrmZJ4kCg+QV/Yy8qRsjiV9\rmPsJeWqhiY5jfO3mC1sEmhb3dzhEW7ntj+xIgCcrlXoU9vkyA/HuPFI=\r"
            };

        public static string[] GetRsaIntermediateCATestCert() => new string[]
            {
                "\rMIIF0TCCA7mgAwIBAgICEAAwDQYJKoZIhvcNAQELBQAwYTELMAkGA1UEBhMCVVMx\rCzAJBgNVBAgMAldBMQswCQYDVQQHDAJzczELMAkGA1UECgwCc3MxCzAJBgNVBAsM\rAnNzMQswCQYDVQQDDAJzczERMA8GCSqGSIb3DQEJARYCc3MwHhcNMTUwNTA1MDAw\rMDAwWhcNMjQxMjMxMjM1OTU5WjBUMQswCQYDVQQDDAJzczELMAkGA1UECAwCV0Ex\rCzAJBgNVBAYTAlVTMREwDwYJKoZIhvcNAQkBFgJzczELMAkGA1UECgwCc3MxCzAJ\rBgNVBAsMAnNzMIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA4SGilBtw\rS9iiCka47TzXHEXZuHPKOvZhs+kepLoL/xWWOo/eOcTfnPnD4M0wR0/+Dm3S6zld\rgdojYQPWU48K6mAlNeaTDF16SiTmuMvUvN0c0aifF+YL+9mZuQY30VMXHrjNTQ/e\rTtjEnrp8aOw08eBxFe5zYA+H9a6SnOXuCczH/X9KXlzcYivxPEDc3ImCC7v0DFsB\r7guWp6ZJpU75I5g6hP/tSn/JxaGA1Rp1quZzM0S1Y4eHxqfuhi6mmMFE2TRBRdBn\r5NFtaAmPSqUS51Ie4ryvUlHWCl4jUjKfbjlaFZexPrKAIa293UiJ5J/oywci81Gq\rboAiafB6gCkXFLARy/5aRV+9NGx/+bHPjGn8Q0/1Izhf+qhw8T494IQDkXcSWHNf\r5hw3EFJyrKFsnyMvhcyjNmXaN2JWbnmjw0v4M0xYUjqSyrHAUZVvc3bFQ/5lR2NJ\rbUuopq2f75TT07jvY0LLd5juqjOOntwuVhFpBEow4iT2ELalTI78RiDEcwdyfp9A\rr9Lom94V8yf1ZdJJg4WL4/1uZFOV4dPKxYtd73AISIXusYaI1bdXmPRbJmCZEdN3\rld3bKeuZisdeQmBNl55bnj0IcLQaESbfq77P",
                "PSppZfU8dy8n4gznMtH9jBqRhU4L\r0hkbeMp6DcLbcC9NBqUImNz9jnCFlxzUEeECAwEAAaOBnzCBnDAPBgNVHRMBAf8E\rBTADAQH/MB0GA1UdDgQWBBS1RST7hEbSqc6iAuny0GZ3AqRhWjAfBgNVHSMEGDAW\rgBQusZAXF6xOrlU/BDkAJLwipDfQszALBgNVHQ8EBAMCAaYwEwYDVR0lBAwwCgYI\rKwYBBQUHAwEwJwYDVR0RBCAwHoINYS5leGFtcGxlLmNvbYINYi5leGFtcGxlLmNv\rbTANBgkqhkiG9w0BAQsFAAOCAgEADhMBwTaOemrA2YfYAz6G5VMKVqoi2buZbaUM\rCWBE4Laj0fjGmBMiDoch5jn4yhvVoLrCf1cWJC/CH2XZqBuxxaayQyeLNJ/z311b\rlrjosRURhmEDgE1SRKfHcN9GdywAsjvmCQkB5j5toBKSzLrRTEoP0fXhaicppHCp\rnUgut2b65Nlj9hYHSkIYujYaFG4vPjJD145yXd+HuwHeMqCunvVm50IsaVoA8OD5\rdg6zPSiecJQFlXIFNGs1kniRmMGOnHMzjM+uUE/uUfrdRiT2e0uq+FPWeYVWlHsO\rRzXYcW5iT23fzp2F6B6tOcACrHt1jMmU7QZVvcAo59aLdSeL+Dbvz8BD8tM5y4mn\rZgH8uIt2VI3uCaj5yVtl58X81//z5w94ihQKpzYZAGklVxCej4npp3g3usQS0ANO\r7bqPN9JVHM5VyxVKyFCSpmwh1cCEoPKJAAm6X/LEgZon6Mq8bBXAiKm06S272umT\rKQ18PjGZWSLJwbhutR2MGCtwbUjIokAWPej6pcmEzxy0wK7xtCMEqiH5hRntHAD8\ry5zTOfJ/XXBb8C56GkDhqjD7lu6gPHZLRYBkRIYHHMQi+xhK9j0k/wRoTiiTwkCw\rctkTDJW4O+im8RrylFeeWTfRsJ7PIDjGp89y+U8aAr5kZEFBT2h6RMDBoSLV9soX\ri4H2KRo=\r"
            };

        public static string GetRsaTestEdgeAgentSignature() => "h+N0UYSXsVACJYgLNMRMWnBYxWyPc7MO+HFFpIm+MRz71T7Z9lfL7COQW8eo3eDi/QUPCkv4ldXB0SZY0GmBCDj7H+IVh7BW6DmxXcj9zzhCzcZFcypoik14tdiE6JywQw/w4jvJu7w+ljFtrPOnzY+LzGNTPqocsVOl/lj4Vj3HlYPqdMocKfJTN6gEYPZ7iKesRzNjarAH44StdoIzvCYRbiHPcuLXNhsaUFZ6yanopCyiYzAgJkdCNbWnEl2wgIqr/0wBe3baDNuOROWNhR4FjfD0AhUR3urvuVnuU6zCeg6cWgESdxnAUrl3TfeVK724kzkMFANY1CeiUyQQrGmwlWanQhCKcKxeKTM7A6TSbvhVo4vcgb6i0apXiFPsjFp991/Vl71MgYVnepdWNmIS0K+B1fw1gkajPgBZARI7v9CS0TTVd00BuvruQRkliXhVXRAQ9132jh4py1pXqSUGMWDd0yXq36Fppm7CflGfDczzARMsYhA5CYAhJ0zDOz6jHJ0prZtqLQeQnbp00H9uQvvNa+8a35LAjGRle4c3AtKGCa427qRe7JS9n2S4YCsdwfQvclHh0+S6sKlCsWS6pqUsBcTplzrp44HuLx18yTpsnnoYe4srj9r/yuBn1xk2AiThPvc4wXAKccl7xY1U2p27ZFMXQIn2t1gyCus=";

        [Theory]
        [Unit]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(TimeoutException))]
        public async Task GetDeploymentConfigInfoAsync_CreateNewModuleClientWhenGetTwinThrowsMoreThanRetryCount(Type thrownException)
        {
            // Arrange
            var moduleClient = new Mock<IModuleClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            var retryStrategy = new Mock<RetryStrategy>(new object[] { false });

            var deploymentConfig = new DeploymentConfig(
                "1.0",
                runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty,
                null);

            var moduleClientProvider = new Mock<IModuleClientProvider>();
            moduleClientProvider.Setup(p => p.Create(It.IsAny<ConnectionStatusChangesHandler>()))
                .ReturnsAsync(moduleClient.Object);

            serde.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(deploymentConfig);
            // var retryStrategy = new FixedInterval(1, TimeSpan.FromMilliseconds(1));
            retryStrategy.Setup(rs => rs.GetShouldRetry())
                .Returns(
                    (int retryCount, Exception lastException, out TimeSpan delay) =>
                    {
                        if (retryCount >= 1)
                        {
                            delay = TimeSpan.Zero;
                            return false;
                        }

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

            var ex = Activator.CreateInstance(thrownException, "msg str") as Exception;
            moduleClient.SetupSequence(d => d.GetTwinAsync())
                .ThrowsAsync(ex)
                .ThrowsAsync(ex)
                .ReturnsAsync(twin);
            moduleClient.Setup(d => d.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>()))
                .Returns(Task.CompletedTask);
            moduleClient.Setup(d => d.SetMethodHandlerAsync(It.IsAny<string>(), It.IsAny<MethodCallback>()))
                .Returns(Task.CompletedTask);

            IEnumerable<IRequestHandler> requestHandlers = new List<IRequestHandler> { new PingRequestHandler() };
            var deviceManager = new Mock<IDeviceManager>();
            IEnumerable<X509Certificate2> manifestTrustBundle = Enumerable.Empty<X509Certificate2>();

            // Act
            IEdgeAgentConnection connection = new EdgeAgentConnection(moduleClientProvider.Object, serde.Object, new RequestManager(requestHandlers, DefaultRequestTimeout), deviceManager.Object, true, TimeSpan.FromHours(1), retryStrategy.Object, Mock.Of<IDeploymentMetrics>(), manifestTrustBundle);

            // Assert
            // The connection hasn't been created yet. So wait for it.
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Act
            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            moduleClient.Verify(m => m.GetTwinAsync(), Times.Exactly(3));
            moduleClient.Verify(m => m.CloseAsync(), Times.Once);
            Assert.Equal(10, deploymentConfigInfo.OrDefault().Version);
            Assert.Equal(deploymentConfigInfo.OrDefault().DeploymentConfig, deploymentConfig);
        }

        [Theory]
        [MemberData(nameof(GetDeploymentForSchemas))]
        public void SchemaVersionCheckTest(DeploymentConfig deployment, Type expectedException)
        {
            if (expectedException != null)
            {
                Assert.Throws(expectedException, () => EdgeAgentConnection.ValidateSchemaVersion(deployment));
            }
            else
            {
                EdgeAgentConnection.ValidateSchemaVersion(deployment);
            }
        }

        public static IEnumerable<object[]> GetDeploymentForSchemas()
        {
            var modulesWithDefaultStartupOrder =
                new Dictionary<string, IModule>
                {
                    { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, null, null) },
                    { "mod2", new TestModule("mod2", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, Constants.DefaultStartupOrder, null, null) }
                };

            var modulesWithCustomStartupOrder =
                new Dictionary<string, IModule>
                {
                    { "mod1", new TestModule("mod1", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, 0, null, null) },
                    { "mod2", new TestModule("mod2", "1.0", "docker", ModuleStatus.Running, new TestConfig("boo"), RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, 10, null, null) }
                };
            var version_0_1 = new DeploymentConfig("0.1", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithDefaultStartupOrder.ToImmutableDictionary(), null);
            var version_1 = new DeploymentConfig("1", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithDefaultStartupOrder.ToImmutableDictionary(), null);
            var version_1_0 = new DeploymentConfig("1.0", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithDefaultStartupOrder.ToImmutableDictionary(), null);
            var version_1_1 = new DeploymentConfig("1.1", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithCustomStartupOrder.ToImmutableDictionary(), null);
            var version_1_1_0 = new DeploymentConfig("1.1.0", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithCustomStartupOrder.ToImmutableDictionary(), null);
            var version_1_2 = new DeploymentConfig("1.2", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithDefaultStartupOrder.ToImmutableDictionary(), null);
            var version_2_0 = new DeploymentConfig("2.0", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithDefaultStartupOrder.ToImmutableDictionary(), null);
            var version_2_0_1 = new DeploymentConfig("2.0.1", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithDefaultStartupOrder.ToImmutableDictionary(), null);
            var version_schema_mismatch = new DeploymentConfig("1.0", UnknownRuntimeInfo.Instance, new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance), modulesWithCustomStartupOrder.ToImmutableDictionary(), null);

            yield return new object[] { version_0_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_1_0, null };
            yield return new object[] { version_1_1, null };
            yield return new object[] { version_1_1_0, null };
            yield return new object[] { version_1_2, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_2_0, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_2_0_1, typeof(InvalidSchemaVersionException) };
            yield return new object[] { version_schema_mismatch, null };
        }

        static async Task SetAgentDesiredProperties(RegistryManager rm, string deviceId)
        {
            var dp = new
            {
                schemaVersion = "1.1.0",
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

        static async Task DeleteConfigurationAsync(RegistryManager registryManager, string configurationId) => await registryManager.RemoveConfigurationAsync(configurationId);

        static async Task UpdateAgentDesiredProperties(RegistryManager rm, string deviceId)
        {
            var dp = new
            {
                schemaVersion = "1.1.0",
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
                schemaVersion = "1.1.0",
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

        static object GetEdgeAgentBaseConfiguration()
        {
            var desiredProperties = new
            {
                schemaVersion = "1.1.0",
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
                    }
                }
            };
            return desiredProperties;
        }

        static object GetEdgeAgentAddOnConfiguration()
        {
            var desiredProperties = new
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
