// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
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
            IServiceClient serviceClient = new ServiceClient(edgeDeviceConnectionString, edgeDeviceId);
            IDeviceClient deviceClient = await DeviceClient.CreateAsync(edgeHubConnectionString, serviceClient);

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
                        { "$version", 10 }
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
            Assert.IsType(typeof(InvalidOperationException), deploymentConfigInfo.OrDefault().Exception.OrDefault());
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
    }
}
