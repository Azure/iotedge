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
    using Microsoft.Azure.Devices.Edge.Storage;
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
        const string EdgeHubModuleId = "$edgeHub";

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
            Devices.IotHubConnectionStringBuilder iotHubConnectionStringBuilder = Devices.IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            (string edgeDeviceId, string deviceConnStr) = await RegistryManagerHelper.CreateDevice("testHubEdgeDevice1", iotHubConnectionString, registryManager, true, false);
            
            string iothubHostName = iotHubConnectionStringBuilder.HostName;
            var identityFactory = new IdentityFactory(iothubHostName);
            string edgeHubConnectionString = $"{deviceConnStr};ModuleId={EdgeHubModuleId}";
            Try<IIdentity> edgeHubIdentity = identityFactory.GetWithConnectionString(edgeHubConnectionString);
            Assert.True(edgeHubIdentity.Success);
            Assert.NotNull(edgeHubIdentity.Value);

            // Set Edge hub desired properties
            await this.SetDesiredProperties(registryManager, edgeDeviceId);

            var endpointFactory = new EndpointFactory(connectionManager, new RoutingMessageConverter(), edgeDeviceId);
            var routeFactory = new EdgeRouteFactory(endpointFactory);

            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            IEntityStore<string, TwinInfo> twinStore = storeProvider.GetEntityStore<string, TwinInfo>("twins");
            var twinManager = new TwinManager(connectionManager, twinCollectionMessageConverter, twinMessageConverter, Option.Some(twinStore));

            // Create Edge Hub connection
            var edgeHubConnection = await EdgeHubConnection.Create(edgeHubIdentity.Value, twinManager, connectionManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter);

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
            EdgeHubConnection.ReportedProperties reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);
            Assert.NotNull(reportedProperties.Clients);
            Assert.Equal(0, reportedProperties.Clients.Count);

            // Simulate a module and a downstream device that connects to Edge Hub.

            string moduleId = "module1";
            string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{edgeDeviceId}/modules/{moduleId}");
            string moduleConnectionstring = $"HostName={iothubHostName};DeviceId={edgeDeviceId};ModuleId={moduleId};SharedAccessSignature={sasToken}";
            Try<IIdentity> moduleIdentity = identityFactory.GetWithConnectionString(moduleConnectionstring);
            IDeviceProxy moduleProxy = Mock.Of<IDeviceProxy>(d => d.IsActive == true);

            string downstreamDeviceId = "device1";
            sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{downstreamDeviceId}");
            string downstreamDeviceConnectionstring = $"HostName={iothubHostName};DeviceId={downstreamDeviceId};SharedAccessSignature={sasToken}";
            Try<IIdentity> downstreamDeviceIdentity = identityFactory.GetWithConnectionString(downstreamDeviceConnectionstring);
            IDeviceProxy downstreamDeviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive == true);

            // Connect the module and downstream device and make sure the reported properties are updated as expected.
            connectionManager.AddDeviceConnection(moduleIdentity.Value, moduleProxy);
            connectionManager.AddDeviceConnection(downstreamDeviceIdentity.Value, downstreamDeviceProxy);
            string moduleIdKey = $"{edgeDeviceId}/{moduleId}";
            await Task.Delay(TimeSpan.FromSeconds(10));
            reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
            Assert.Equal(2, reportedProperties.Clients.Count);
            Assert.Equal(ConnectionStatus.Connected, reportedProperties.Clients[moduleIdKey].Status);
            Assert.NotNull(reportedProperties.Clients[moduleIdKey].LastConnectedTimeUtc);
            Assert.Null(reportedProperties.Clients[moduleIdKey].LastDisconnectTimeUtc);
            Assert.Equal(ConnectionStatus.Connected, reportedProperties.Clients[downstreamDeviceId].Status);
            Assert.NotNull(reportedProperties.Clients[downstreamDeviceId].LastConnectedTimeUtc);
            Assert.Null(reportedProperties.Clients[downstreamDeviceId].LastDisconnectTimeUtc);
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
            await this.UpdateDesiredProperties(registryManager, edgeDeviceId);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(callbackCalled);

            reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);
            Assert.NotNull(reportedProperties.Clients);
            Assert.Equal(2, reportedProperties.Clients.Count);

            // Disconnect the downstream device and make sure the reported properties are updated as expected.
            await connectionManager.RemoveDeviceConnection(moduleIdKey);
            await connectionManager.RemoveDeviceConnection(downstreamDeviceId);
            await Task.Delay(TimeSpan.FromSeconds(10));
            reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
            Assert.Equal(1, reportedProperties.Clients.Count);
            Assert.True(reportedProperties.Clients.ContainsKey(moduleIdKey));
            Assert.False(reportedProperties.Clients.ContainsKey(downstreamDeviceId));
            Assert.Equal(ConnectionStatus.Disconnected, reportedProperties.Clients[moduleIdKey].Status);
            Assert.NotNull(reportedProperties.Clients[moduleIdKey].LastConnectedTimeUtc);
            Assert.NotNull(reportedProperties.Clients[moduleIdKey].LastDisconnectTimeUtc);
            Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);

            // If the edge hub restarts, clear out the connected devices in the reported properties.
            edgeHubConnection = await EdgeHubConnection.Create(edgeHubIdentity.Value, twinManager, connectionManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter);
            reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
            Assert.Null(reportedProperties.Clients);

            await RegistryManagerHelper.RemoveDevice(edgeDeviceId, registryManager);
        }

        async Task<EdgeHubConnection.ReportedProperties> GetReportedProperties(RegistryManager registryManager, string edgeHubId)
        {
            Twin twin = await registryManager.GetTwinAsync(edgeHubId, EdgeHubModuleId);
            string reportedPropertiesJson = twin.Properties.Reported.ToJson();
            var reportedProperties = JsonConvert.DeserializeObject<EdgeHubConnection.ReportedProperties>(reportedPropertiesJson);
            return reportedProperties;
        }

        async Task SetDesiredProperties(RegistryManager registryManager, string edgeDeviceId)
        {
            ConfigurationContent cc = new ConfigurationContent() { ModuleContent = new Dictionary<string, TwinContent>() };
            var twinContent = new TwinContent();
            cc.ModuleContent["$edgeHub"] = twinContent;

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

            twinContent.TargetContent = new TwinCollection(patch);
            await registryManager.ApplyConfigurationContentOnDeviceAsync(edgeDeviceId, cc);
        }

        async Task UpdateDesiredProperties(RegistryManager registryManager, string edgeDeviceId)
        {
            ConfigurationContent cc = new ConfigurationContent() { ModuleContent = new Dictionary<string, TwinContent>() };
            var twinContent = new TwinContent();
            cc.ModuleContent["$edgeHub"] = twinContent;

            var desiredProperties = new
            {
                schemaVersion = "1.0",
                routes = new Dictionary<string, string>
                {
                    ["route1"] = "from /* INTO $upstream",
                    ["route2"] = "from /modules/module1 INTO BrokeredEndpoint(\"/modules/Module2/inputs/input1\")",
                },
                storeAndForwardConfiguration = new
                {
                    timeToLiveSecs = 20
                }
            };

            string patch = JsonConvert.SerializeObject(desiredProperties);
            twinContent.TargetContent = new TwinCollection(patch);
            await registryManager.ApplyConfigurationContentOnDeviceAsync(edgeDeviceId, cc);
        }        
    }
}
