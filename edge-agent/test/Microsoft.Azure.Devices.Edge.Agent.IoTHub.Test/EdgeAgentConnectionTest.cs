// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Xunit;

    [Bvt]
    public class EdgeAgentConnectionTest
    {
        const string DockerType = "docker";

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
                        configuration = new
                        {
                            id = "1235"
                        },
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
                        configuration = new
                        {
                            id = "1235"
                        },
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
                        configuration = new
                        {
                            id = "1235"
                        },
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
                        configuration = new
                        {
                            id = "1236"
                        },
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
                        configuration = new
                        {
                            id = "1236"
                        },
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
    }
}
