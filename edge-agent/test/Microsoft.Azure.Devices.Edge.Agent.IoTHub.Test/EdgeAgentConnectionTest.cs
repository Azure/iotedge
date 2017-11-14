// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class EdgeAgentConnectionTest
    {
        const string DockerType = "docker";

        [Bvt]
        [Fact]
        public async Task EdgeAgentConnectionBasicTest()
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid().ToString();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };
            edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

            await SetAgentDesiredProperties(registryManager, edgeDeviceId);

            string edgeDeviceConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
            EdgeHubConnectionString edgeHubConnectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(iotHubConnectionStringBuilder.HostName, edgeDeviceId)
                .SetSharedAccessKey(edgeDevice.Authentication.SymmetricKey.PrimaryKey)
                .Build();
            IDeviceClient deviceClient = DeviceClient.Create(edgeHubConnectionString);

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
            IEdgeAgentConnection edgeAgentConnection = await EdgeAgentConnection.Create(deviceClient, serde);
            await Task.Delay(TimeSpan.FromSeconds(10));

            Option<DeploymentConfigInfo> deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

            Assert.True(deploymentConfigInfo.HasValue);
            DeploymentConfig deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
            Assert.NotNull(deploymentConfig);
            Assert.NotNull(deploymentConfig.Modules);
            Assert.NotNull(deploymentConfig.Runtime);
            Assert.NotNull(deploymentConfig.SystemModules);
            Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion, deploymentConfig.SchemaVersion);
            Assert.NotNull(deploymentConfig.SystemModules.EdgeAgent);
            Assert.NotNull(deploymentConfig.SystemModules.EdgeHub);
            Assert.Equal(1, deploymentConfig.Modules.Count);
            Assert.NotNull(deploymentConfig.Modules["mongoserver"]);

            await UpdateAgentDesiredProperties(registryManager, edgeDeviceId);
            await Task.Delay(TimeSpan.FromSeconds(10));

            deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

            Assert.True(deploymentConfigInfo.HasValue);
            deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
            Assert.NotNull(deploymentConfig);
            Assert.NotNull(deploymentConfig.Modules);
            Assert.NotNull(deploymentConfig.Runtime);
            Assert.NotNull(deploymentConfig.SystemModules);
            Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion, deploymentConfig.SchemaVersion);
            Assert.NotNull(deploymentConfig.SystemModules.EdgeAgent);
            Assert.NotNull(deploymentConfig.SystemModules.EdgeHub);
            Assert.Equal(2, deploymentConfig.Modules.Count);
            Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
            Assert.NotNull(deploymentConfig.Modules["mlModule"]);

            await registryManager.RemoveDeviceAsync(edgeDevice);
        }

        public static async Task SetAgentDesiredProperties(RegistryManager rm, string deviceId)
        {
            ConfigurationContent cc = new ConfigurationContent() { ModuleContent = new Dictionary<string, TwinContent>() };
            var twinContent = new TwinContent();
            cc.ModuleContent["$edgeAgent"] = twinContent;
            var dp = new
            {
                schemaVersion = "1.0",
                runtime = new
                {
                    type = "docker",
                    settings = new
                    {
                        minDockerVersion = "1.5",
                        loggingOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
                        }
                    }
                }
            };
            string patch = JsonConvert.SerializeObject(dp);
            twinContent.TargetContent = new TwinCollection(patch);
            await rm.ApplyConfigurationContentOnDeviceAsync(deviceId, cc);
        }

        [Bvt]
        [Fact]
        public async Task EdgeAgentConnectionConfigurationTest()
        {
            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid().ToString();
            string configurationId = "testconfiguration-" + Guid.NewGuid().ToString();
            string conditionPropertyName = "condition-" + Guid.NewGuid().ToString("N");
            string conditionPropertyValue = Guid.NewGuid().ToString();
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            try
            {
                await registryManager.OpenAsync();

                var edgeDevice = new Device(edgeDeviceId)
                {
                    Capabilities = new DeviceCapabilities { IotEdge = true },
                    Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
                };
                edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

                var twin = await registryManager.GetTwinAsync(edgeDeviceId);
                twin.Tags[conditionPropertyName] = conditionPropertyValue;
                await registryManager.UpdateTwinAsync(edgeDeviceId, twin, twin.ETag);
                await registryManager.GetTwinAsync(edgeDeviceId, "$edgeAgent");
                await registryManager.GetTwinAsync(edgeDeviceId, "$edgeHub");

                await CreateConfigurationAsync(registryManager, configurationId, $"tags.{conditionPropertyName}='{conditionPropertyValue}'", 10);

                await Task.Delay(TimeSpan.FromSeconds(45));

                string edgeDeviceConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
                EdgeHubConnectionString edgeHubConnectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(iotHubConnectionStringBuilder.HostName, edgeDeviceId)
                    .SetSharedAccessKey(edgeDevice.Authentication.SymmetricKey.PrimaryKey)
                    .Build();
                IDeviceClient deviceClient = DeviceClient.Create(edgeHubConnectionString);

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
                IEdgeAgentConnection edgeAgentConnection = await EdgeAgentConnection.Create(deviceClient, serde);
                await Task.Delay(TimeSpan.FromSeconds(10));

                Option<DeploymentConfigInfo> deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                Assert.True(deploymentConfigInfo.HasValue);
                DeploymentConfig deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
                Assert.NotNull(deploymentConfig);
                Assert.NotNull(deploymentConfig.Modules);
                Assert.NotNull(deploymentConfig.Runtime);
                Assert.NotNull(deploymentConfig.SystemModules);
                Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion, deploymentConfig.SchemaVersion);
                Assert.NotNull(deploymentConfig.SystemModules.EdgeAgent);
                Assert.Equal(configurationId, deploymentConfig.SystemModules.EdgeAgent.ConfigurationInfo.Id);
                Assert.NotNull(deploymentConfig.SystemModules.EdgeHub);
                Assert.Equal(configurationId, deploymentConfig.SystemModules.EdgeHub.ConfigurationInfo.Id);
                Assert.Equal(2, deploymentConfig.Modules.Count);
                Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
                Assert.Equal(configurationId, deploymentConfig.Modules["mongoserver"].ConfigurationInfo.Id);
                Assert.NotNull(deploymentConfig.Modules["asa"]);
                Assert.Equal(configurationId, deploymentConfig.Modules["asa"].ConfigurationInfo.Id);

                var reportedPatch = GetEdgeAgentReportedProperties(deploymentConfigInfo.OrDefault());
                await edgeAgentConnection.UpdateReportedPropertiesAsync(reportedPatch);
                await Task.Delay(TimeSpan.FromSeconds(45));

                var config = await registryManager.GetConfigurationAsync(configurationId);
                Assert.NotNull(config);
                Assert.NotNull(config.Statistics);
                Assert.True(config.Statistics.ContainsKey("targetedCount"));
                Assert.Equal(1, config.Statistics["targetedCount"]);
                Assert.True(config.Statistics.ContainsKey("appliedCount"));
                Assert.Equal(1, config.Statistics["appliedCount"]);
                Assert.True(config.Statistics.ContainsKey("reportedSuccessfulCount"));
                Assert.Equal(1, config.Statistics["reportedSuccessfulCount"]);
            }
            finally
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(edgeDeviceId);
                }
                catch (Exception)
                {
                }

                try
                {
                    await DeleteConfigurationAsync(registryManager, configurationId);
                }
                catch (Exception)
                {
                }
            }
        }

        public static async Task<Configuration> CreateConfigurationAsync(RegistryManager registryMananger, string configurationId, string targetCondition, int priority)
        {
            Configuration configuration = new Configuration(configurationId)
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

        public static async Task DeleteConfigurationAsync(RegistryManager registryManager, string configurationId)
        {
            await registryManager.RemoveConfigurationAsync(configurationId);
        }

        public static TwinCollection GetEdgeAgentReportedProperties(DeploymentConfigInfo deploymentConfigInfo)
        {
            var deploymentConfig = deploymentConfigInfo.DeploymentConfig;
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
                            id = deploymentConfig.SystemModules.EdgeAgent.ConfigurationInfo.Id
                        }
                    },
                    edgeHub = new
                    {
                        runtimeStatus = "running",
                        description = "All good",
                        configuration = new
                        {
                            id = deploymentConfig.SystemModules.EdgeHub.ConfigurationInfo.Id
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
            return new ConfigurationContent()
            {
                ModuleContent = new Dictionary<string, TwinContent>
                {
                    { "$edgeAgent", GetEdgeAgentConfiguration() },
                    { "$edgeHub", GetEdgeHubConfiguration() },
                    { "mongoserver", GetTwinConfiguration("mongoserver") },
                    { "asa", GetTwinConfiguration("asa") }
                }
            };
        }

        static TwinContent GetEdgeAgentConfiguration()
        {
            TwinContent edgeAgent = new TwinContent();
            var desiredProperties = new
            {
                schemaVersion = "1.0",
                runtime = new
                {
                    type = "docker",
                    settings = new
                    {
                        minDockerVersion = "1.5",
                        loggingOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
                        }
                    }
                }
            };
            string patch = JsonConvert.SerializeObject(desiredProperties);
            edgeAgent.TargetContent = new TwinCollection(patch);

            return edgeAgent;
        }

        static TwinContent GetEdgeHubConfiguration()
        {
            TwinContent edgeHub = new TwinContent();
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
            string patch = JsonConvert.SerializeObject(desiredProperties);

            edgeHub.TargetContent = new TwinCollection(patch);

            return edgeHub;
        }

        static TwinContent GetTwinConfiguration(string moduleName)
        {
            TwinContent configuration = new TwinContent();
            configuration.TargetContent = new TwinCollection();
            configuration.TargetContent["name"] = moduleName;
            return configuration;
        }

        public static async Task UpdateAgentDesiredProperties(RegistryManager rm, string deviceId)
        {
            ConfigurationContent cc = new ConfigurationContent() { ModuleContent = new Dictionary<string, TwinContent>() };
            var twinContent = new TwinContent();
            cc.ModuleContent["$edgeAgent"] = twinContent;
            var dp = new
            {
                schemaVersion = "1.0",
                runtime = new
                {
                    type = "docker",
                    settings = new
                    {
                        minDockerVersion = "1.5",
                        loggingOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
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
                            createOptions = ""
                        }
                    }
                }
            };
            string patch = JsonConvert.SerializeObject(dp);
            twinContent.TargetContent = new TwinCollection(patch);
            await rm.ApplyConfigurationContentOnDeviceAsync(deviceId, cc);
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncReturnsConfigWhenThereAreNoErrors()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            Client.ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            Client.DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
            object desiredPropertyUpdateCallbackContext;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
                    {
                        { "$version", 10 },

                        // This is here to prevent the "empty" twin error from being thrown.
                        { "MoreStuff", "MoreStuffHereToo" }
                    }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "1.0", runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty
            );

            deviceClient.Setup(d => d.SetConnectionStatusChangedHandler(It.IsAny<Client.ConnectionStatusChangesHandler>()))
                .Callback<Client.ConnectionStatusChangesHandler>(handler => connectionStatusChangesHandler = handler);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallback(It.IsAny<Client.DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<Client.DesiredPropertyUpdateCallback, object>((handler, context) =>
                {
                    desiredPropertyUpdateCallback = handler;
                    desiredPropertyUpdateCallbackContext = context;
                })
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            // Act
            var connection = await EdgeAgentConnection.Create(deviceClient.Object, serde.Object);
            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(Client.ConnectionStatus.Connected, Client.ConnectionStatusChangeReason.Connection_Ok);

            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.Equal(deploymentConfigInfo.OrDefault().Version, 10);
            Assert.Equal(deploymentConfigInfo.OrDefault().DeploymentConfig, deploymentConfig);
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncIncludesExceptionWhenDeserializeThrows()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            Client.ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            Client.DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
            object desiredPropertyUpdateCallbackContext;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
                    {
                        { "$version", 10 },

                        // This is here to prevent the "empty" twin error from being thrown.
                        { "MoreStuff", "MoreStuffHereToo" }
                    }).ToString()),
                    Reported = new TwinCollection()
                }
            };

            deviceClient.Setup(d => d.SetConnectionStatusChangedHandler(It.IsAny<Client.ConnectionStatusChangesHandler>()))
                .Callback<Client.ConnectionStatusChangesHandler>(handler => connectionStatusChangesHandler = handler);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallback(It.IsAny<Client.DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<Client.DesiredPropertyUpdateCallback, object>((handler, context) =>
                {
                    desiredPropertyUpdateCallback = handler;
                    desiredPropertyUpdateCallbackContext = context;
                })
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Throws<FormatException>();

            // Act
            var connection = await EdgeAgentConnection.Create(deviceClient.Object, serde.Object);
            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(Client.ConnectionStatus.Connected, Client.ConnectionStatusChangeReason.Connection_Ok);

            var deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType(typeof(ConfigFormatException), deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncIncludesExceptionWhenDeserializeThrowsConfigEmptyException()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            Client.ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            Client.DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
            object desiredPropertyUpdateCallbackContext;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
                    {
                        { "$version", 10 }
                    }).ToString()),
                    Reported = new TwinCollection()
                }
            };

            deviceClient.Setup(d => d.SetConnectionStatusChangedHandler(It.IsAny<Client.ConnectionStatusChangesHandler>()))
                .Callback<Client.ConnectionStatusChangesHandler>(handler => connectionStatusChangesHandler = handler);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallback(It.IsAny<Client.DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<Client.DesiredPropertyUpdateCallback, object>((handler, context) =>
                {
                    desiredPropertyUpdateCallback = handler;
                    desiredPropertyUpdateCallbackContext = context;
                })
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            // Act
            var connection = await EdgeAgentConnection.Create(deviceClient.Object, serde.Object);
            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(Client.ConnectionStatus.Connected, Client.ConnectionStatusChangeReason.Connection_Ok);

            var deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType(typeof(ConfigEmptyException), deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoIncludesExceptionWhenSchemaVersionDoesNotMatch()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            Client.ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            Client.DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
            object desiredPropertyUpdateCallbackContext;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
                    {
                        { "$version", 10 },

                        // This is here to prevent the "empty" twin error from being thrown.
                        { "MoreStuff", "MoreStuffHereToo" }
                    }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "InvalidSchemaVersion", runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty
            );

            deviceClient.Setup(d => d.SetConnectionStatusChangedHandler(It.IsAny<Client.ConnectionStatusChangesHandler>()))
                .Callback<Client.ConnectionStatusChangesHandler>(handler => connectionStatusChangesHandler = handler);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallback(It.IsAny<Client.DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<Client.DesiredPropertyUpdateCallback, object>((handler, context) =>
                {
                    desiredPropertyUpdateCallback = handler;
                    desiredPropertyUpdateCallbackContext = context;
                })
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            // Act
            var connection = await EdgeAgentConnection.Create(deviceClient.Object, serde.Object);
            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(Client.ConnectionStatus.Connected, Client.ConnectionStatusChangeReason.Connection_Ok);
            var deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType(typeof(InvalidSchemaVersionException), deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncIncludesExceptionWhenGetTwinThrows()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            Client.ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            Client.DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
            object desiredPropertyUpdateCallbackContext;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
                    {
                        { "$version", 10 }
                    }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "1.0", runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty
            );

            deviceClient.Setup(d => d.SetConnectionStatusChangedHandler(It.IsAny<Client.ConnectionStatusChangesHandler>()))
                .Callback<Client.ConnectionStatusChangesHandler>(handler => connectionStatusChangesHandler = handler);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallback(It.IsAny<Client.DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<Client.DesiredPropertyUpdateCallback, object>((handler, context) =>
                {
                    desiredPropertyUpdateCallback = handler;
                    desiredPropertyUpdateCallbackContext = context;
                })
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.GetTwinAsync())
                .ThrowsAsync(new InvalidOperationException());

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            // Act
            var connection = await EdgeAgentConnection.Create(deviceClient.Object, serde.Object);
            Assert.NotNull(connectionStatusChangesHandler);
            connectionStatusChangesHandler.Invoke(Client.ConnectionStatus.Connected, Client.ConnectionStatusChangeReason.Connection_Ok);
            var deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.True(deploymentConfigInfo.OrDefault().Exception.HasValue);
            Assert.IsType(typeof(InvalidOperationException), deploymentConfigInfo.OrDefault().Exception.OrDefault());
        }

        [Fact]
        [Unit]
        public async Task GetDeploymentConfigInfoAsyncReturnsConfigWhenThereAreNoErrorsWithPatch()
        {
            // Arrange
            var deviceClient = new Mock<IDeviceClient>();
            var serde = new Mock<ISerde<DeploymentConfig>>();
            var runtime = new Mock<IRuntimeInfo>();
            var edgeAgent = new Mock<IEdgeAgentModule>();
            var edgeHub = new Mock<IEdgeHubModule>();
            Client.ConnectionStatusChangesHandler connectionStatusChangesHandler = null;
            Client.DesiredPropertyUpdateCallback desiredPropertyUpdateCallback = null;
            object desiredPropertyUpdateCallbackContext;
            var twin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
                    {
                        { "$version", 10 },

                        // This is here to prevent the "empty" twin error from being thrown.
                        { "MoreStuff", "MoreStuffHereToo" }
                    }).ToString()),
                    Reported = new TwinCollection()
                }
            };
            var deploymentConfig = new DeploymentConfig(
                "1.0", runtime.Object,
                new SystemModules(edgeAgent.Object, edgeHub.Object),
                ImmutableDictionary<string, IModule>.Empty
            );

            deviceClient.Setup(d => d.SetConnectionStatusChangedHandler(It.IsAny<Client.ConnectionStatusChangesHandler>()))
                .Callback<Client.ConnectionStatusChangesHandler>(handler => connectionStatusChangesHandler = handler);
            deviceClient.Setup(d => d.SetDesiredPropertyUpdateCallback(It.IsAny<Client.DesiredPropertyUpdateCallback>(), It.IsAny<object>()))
                .Callback<Client.DesiredPropertyUpdateCallback, object>((handler, context) =>
                {
                    desiredPropertyUpdateCallback = handler;
                    desiredPropertyUpdateCallbackContext = context;
                })
                .Returns(Task.CompletedTask);
            deviceClient.Setup(d => d.GetTwinAsync())
                .ReturnsAsync(twin);

            serde.Setup(s => s.Deserialize(It.IsAny<string>()))
                .Returns(deploymentConfig);

            var connection = await EdgeAgentConnection.Create(deviceClient.Object, serde.Object);
            Assert.NotNull(connectionStatusChangesHandler);

            // this will cause the initial desired props to get set in the connection object
            connectionStatusChangesHandler.Invoke(Client.ConnectionStatus.Connected, Client.ConnectionStatusChangeReason.Connection_Ok);

            // Act
            // now send a patch update
            var patch = new TwinCollection(JObject.FromObject(new Dictionary<string, object>
            {
                { "$version", 11 }
            }).ToString());
            await desiredPropertyUpdateCallback.Invoke(patch, null);

            Option<DeploymentConfigInfo> deploymentConfigInfo = await connection.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.True(deploymentConfigInfo.HasValue);
            Assert.Equal(deploymentConfigInfo.OrDefault().Version, 11);
            Assert.Equal(deploymentConfigInfo.OrDefault().DeploymentConfig, deploymentConfig);
        }

        [Bvt]
        [Fact(Skip = "Connected status update in IoTHub takes 5 mins.")]
        public async Task EdgeAgentConnectionStatusTest()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid().ToString();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };
            edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

            await SetAgentDesiredProperties(registryManager, edgeDeviceId);

            string edgeDeviceConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
            EdgeHubConnectionString edgeHubConnectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(iotHubConnectionStringBuilder.HostName, edgeDeviceId)
                .SetSharedAccessKey(edgeDevice.Authentication.SymmetricKey.PrimaryKey)
                .Build();
            IDeviceClient deviceClient = DeviceClient.Create(edgeHubConnectionString);

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

            // Assert
            Module edgeAgentModule = await registryManager.GetModuleAsync(edgeDevice.Id, Constants.EdgeAgentModuleIdentityName);
            Assert.NotNull(edgeAgentModule);
            Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Disconnected);

            IEdgeAgentConnection edgeAgentConnection = await EdgeAgentConnection.Create(deviceClient, serde);
            await Task.Delay(TimeSpan.FromMinutes(7));

            edgeAgentModule = await registryManager.GetModuleAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName);
            Assert.NotNull(edgeAgentModule);
            Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Connected);

            edgeAgentConnection.Dispose();
            await Task.Delay(TimeSpan.FromMinutes(7));

            edgeAgentModule = await registryManager.GetModuleAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName);
            Assert.NotNull(edgeAgentModule);
            Assert.True(edgeAgentModule.ConnectionState == DeviceConnectionState.Disconnected);
        }

        [Bvt]
        [Fact]
        public async Task EdgeAgentPingMethodTest()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid().ToString();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };
            edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

            await SetAgentDesiredProperties(registryManager, edgeDeviceId);

            string edgeDeviceConnectionString = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeDeviceId};SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
            EdgeHubConnectionString edgeHubConnectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(iotHubConnectionStringBuilder.HostName, edgeDeviceId)
                .SetSharedAccessKey(edgeDevice.Authentication.SymmetricKey.PrimaryKey)
                .Build();
            IDeviceClient deviceClient = DeviceClient.Create(edgeHubConnectionString);

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
            Devices.ServiceClient serviceClient = Devices.ServiceClient.CreateFromConnectionString(iotHubConnectionString);

            // Assert
            await Assert.ThrowsAsync<DeviceNotFoundException>(() => serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping")));

            IEdgeAgentConnection edgeAgentConnection = await EdgeAgentConnection.Create(deviceClient, serde);
            await Task.Delay(TimeSpan.FromSeconds(5));

            CloudToDeviceMethodResult methodResult = await serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping"));
            Assert.NotNull(methodResult);
            Assert.Equal(200, methodResult.Status);

            CloudToDeviceMethodResult invalidMethodResult = await serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("poke"));
            Assert.NotNull(invalidMethodResult);
            Assert.Equal(501, invalidMethodResult.Status);

            edgeAgentConnection.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAsync<DeviceNotFoundException>(() => serviceClient.InvokeDeviceMethodAsync(edgeDeviceId, Constants.EdgeAgentModuleIdentityName, new CloudToDeviceMethod("ping")));
        }
    }
}
