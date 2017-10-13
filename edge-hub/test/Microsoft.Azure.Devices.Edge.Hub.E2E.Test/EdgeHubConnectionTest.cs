// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Bvt]
    public class EdgeHubConnectionTest
    {
        [Fact]
        public async Task TestEdgeHubConnection()
        {
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var messageConverterProvider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Client.Message), new MqttMessageConverter() },
                    { typeof(Twin), twinMessageConverter },
                    { typeof(TwinCollection), twinCollectionMessageConverter }
                });
            var cloudProxyProvider = new CloudProxyProvider(messageConverterProvider, 1, true);
            var connectionManager = new ConnectionManager(cloudProxyProvider);
           
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeHubId = "testEdgeDevice21";
            Device edgeDevice = await registryManager.GetDeviceAsync(edgeHubId);
            if (edgeDevice != null)
            {
                await registryManager.RemoveDeviceAsync(edgeDevice);
            }
            edgeDevice = await registryManager.AddDeviceAsync(new Device(edgeHubId));

            Devices.IotHubConnectionStringBuilder iotHubConnectionStringBuilder = Devices.IotHubConnectionStringBuilder.Create(iotHubConnectionString);

            var identityFactory = new IdentityFactory(iotHubConnectionStringBuilder.HostName);
            string deviceConnStr = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={edgeHubId};SharedAccessKey={edgeDevice.Authentication.SymmetricKey.PrimaryKey}";
            Try<IIdentity> edgeHubIdentity = identityFactory.GetWithSasToken(deviceConnStr);
            Assert.True(edgeHubIdentity.Success);
            Assert.NotNull(edgeHubIdentity.Value);

            // Set Edge hub desired properties
            long expectedVersion = await this.SetDesiredProperties(registryManager, edgeHubId);

            var endpointFactory = new EndpointFactory(connectionManager, new RoutingMessageConverter(), edgeHubId);
            var routeFactory = new EdgeRouteFactory(endpointFactory);

            // Create Edge Hub connection
            var edgeHubConnection = await EdgeHubConnection.Create(edgeHubIdentity.Value, connectionManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter);

            // Get and Validate EdgeHubConfig
            EdgeHubConfig edgeHubConfig = await edgeHubConnection.GetConfig();
            Assert.NotNull(edgeHubConfig);
            Assert.Equal("1.0", edgeHubConfig.SchemaVersion);
            Assert.NotNull(edgeHubConfig.Routes);
            Assert.NotNull(edgeHubConfig.StoreAndForwardConfiguration);
            Assert.Equal(20, edgeHubConfig.StoreAndForwardConfiguration.TimeToLiveSecs);

            Route route = edgeHubConfig.Routes["route1"];
            Assert.NotNull(route);
            Assert.True(route.Endpoints.First().GetType() == typeof(CloudEndpoint));

            // Make sure reported properties were updated appropriately
            EdgeHubConnection.ReportedProperties reportedProperties = await this.GetReportedProperties(registryManager, edgeHubId);
            Assert.Equal(expectedVersion, reportedProperties.LastDesiredVersion.Value);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);
            Assert.NotNull(reportedProperties.Clients);
            Assert.Equal(0, reportedProperties.Clients.Count);

            // Simulate a downstream device that connects to Edge Hub.
            string downstreamDeviceId = "device1";
            string sasToken = TokenHelper.CreateSasToken($"{iotHubConnectionStringBuilder.HostName}/devices/{downstreamDeviceId}");
            string downstreamDeviceConnectionstring = $"HostName={iotHubConnectionStringBuilder.HostName};DeviceId={downstreamDeviceId};SharedAccessSignature={sasToken}";
            Try<IIdentity> downstreamDeviceIdentity = identityFactory.GetWithSasToken(downstreamDeviceConnectionstring);
            IDeviceProxy downstreamDeviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive == true);

            // Connect the downstream device and make sure the reported properties are updated as expected.
            connectionManager.AddDeviceConnection(downstreamDeviceIdentity.Value, downstreamDeviceProxy);
            await Task.Delay(TimeSpan.FromSeconds(2));
            reportedProperties = await this.GetReportedProperties(registryManager, edgeHubId);
            Assert.Equal(1, reportedProperties.Clients.Count);
            Assert.Equal(ConnectionStatus.Connected, reportedProperties.Clients[downstreamDeviceId].Status);
            Assert.NotNull(reportedProperties.Clients[downstreamDeviceId].LastConnectedTimeUtc);
            Assert.Null(reportedProperties.Clients[downstreamDeviceId].LastDisconnectTimeUtc);
            Assert.Equal(expectedVersion, reportedProperties.LastDesiredVersion.Value);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);

            // Update desired propertied and make sure callback is called with valid values
            bool callbackCalled = false;
            Task ConfigUpdatedCallback(EdgeHubConfig updatedConfig)
            {
                Assert.NotNull(updatedConfig);
                Assert.NotNull(updatedConfig.StoreAndForwardConfiguration);
                Assert.NotNull(updatedConfig.Routes);
                Assert.Equal(2, updatedConfig.Routes.Count);
                Route route2 = edgeHubConfig.Routes["route2"];
                Assert.NotNull(route2);
                Assert.True(route2.Endpoints.First().GetType() == typeof(ModuleEndpoint));
                callbackCalled = true;
                return Task.CompletedTask;
            }

            edgeHubConnection.SetConfigUpdatedCallback(ConfigUpdatedCallback);
            expectedVersion = await this.UpdateDesiredProperties(registryManager, edgeHubId);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(callbackCalled);

            reportedProperties = await this.GetReportedProperties(registryManager, edgeHubId);
            Assert.Equal(expectedVersion, reportedProperties.LastDesiredVersion.Value);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);
            Assert.NotNull(reportedProperties.Clients);
            Assert.Equal(1, reportedProperties.Clients.Count);

            // Disconnect the downstream device and make sure the reported properties are updated as expected.
            await connectionManager.RemoveDeviceConnection(downstreamDeviceId);
            await Task.Delay(TimeSpan.FromSeconds(2));
            reportedProperties = await this.GetReportedProperties(registryManager, edgeHubId);
            Assert.Equal(1, reportedProperties.Clients.Count);
            Assert.Equal(ConnectionStatus.Disconnected, reportedProperties.Clients[downstreamDeviceId].Status);
            Assert.NotNull(reportedProperties.Clients[downstreamDeviceId].LastConnectedTimeUtc);
            Assert.NotNull(reportedProperties.Clients[downstreamDeviceId].LastDisconnectTimeUtc);
            Assert.Equal(expectedVersion, reportedProperties.LastDesiredVersion.Value);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);

            // If the edge hub restarts, clear out the connected devices in the reported properties.
            edgeHubConnection = await EdgeHubConnection.Create(edgeHubIdentity.Value, connectionManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter);
            reportedProperties = await this.GetReportedProperties(registryManager, edgeHubId);
            Assert.Null(reportedProperties.Clients);

            await registryManager.RemoveDeviceAsync(edgeHubId);
        }

        async Task<EdgeHubConnection.ReportedProperties> GetReportedProperties(RegistryManager registryManager, string edgeHubId)
        {
            Twin twin = await registryManager.GetTwinAsync(edgeHubId);
            string reportedPropertiesJson = twin.Properties.Reported.ToJson();
            var reportedProperties = JsonConvert.DeserializeObject<EdgeHubConnection.ReportedProperties>(reportedPropertiesJson);
            return reportedProperties;
        }

        async Task<long> SetDesiredProperties(RegistryManager registryManager, string edgeHubId)
        {
            Twin twin = await registryManager.GetTwinAsync(edgeHubId);
            var desiredProperties = new
            {
                properties = new
                {
                    desired = new
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
                    }
                }
            };
            string patch = JsonConvert.SerializeObject(desiredProperties);
            Twin updatedTwin = await registryManager.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);
            return updatedTwin.Properties.Desired.Version;
        }

        async Task<long> UpdateDesiredProperties(RegistryManager registryManager, string edgeHubId)
        {
            Twin twin = await registryManager.GetTwinAsync(edgeHubId);
            var desiredProperties = new
            {
                properties = new
                {
                    desired = new
                    {
                        routes = new Dictionary<string, string>
                        {
                            ["route2"] = "from /modules/module1 INTO BrokeredEndpoint(\"/modules/Module2/inputs/input1\")",
                        }
                    }
                }
            };
            string patch = JsonConvert.SerializeObject(desiredProperties);
            Twin updatedTwin = await registryManager.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);
            return updatedTwin.Properties.Desired.Version;
        }
    }
}
