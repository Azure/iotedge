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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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

    [E2E]
    public class EdgeHubConnectionTest
    {
        const string EdgeHubModuleId = "$edgeHub";

        [Fact]
        public async Task TestEdgeHubConnection()
        {
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var messageConverterProvider = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Client.Message), new MqttMessageConverter() },
                    { typeof(Twin), twinMessageConverter },
                    { typeof(TwinCollection), twinCollectionMessageConverter }
                });
            var cloudConnectionProvider = new CloudConnectionProvider(messageConverterProvider, 1, new DeviceClientProvider());
            var connectionManager = new ConnectionManager(cloudConnectionProvider);

            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            Devices.IotHubConnectionStringBuilder iotHubConnectionStringBuilder = Devices.IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            (string edgeDeviceId, string deviceConnStr) = await RegistryManagerHelper.CreateDevice("testHubEdgeDevice1", iotHubConnectionString, registryManager, true, false);

            try
            {

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

                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // Create Edge Hub connection
                EdgeHubConnection edgeHubConnection = await EdgeHubConnection.Create(
                    edgeHubIdentity.Value,
                    twinManager,
                    connectionManager,
                    routeFactory,
                    twinCollectionMessageConverter,
                    twinMessageConverter,
                    versionInfo
                );

                // Get and Validate EdgeHubConfig
                Option<EdgeHubConfig> edgeHubConfigOption = await edgeHubConnection.GetConfig();
                Assert.True(edgeHubConfigOption.HasValue);
                EdgeHubConfig edgeHubConfig = edgeHubConfigOption.OrDefault();
                Assert.Equal("1.0", edgeHubConfig.SchemaVersion);
                Assert.NotNull(edgeHubConfig.Routes);
                Assert.Equal(4, edgeHubConfig.Routes.Count);
                Assert.NotNull(edgeHubConfig.StoreAndForwardConfiguration);
                Assert.Equal(20, edgeHubConfig.StoreAndForwardConfiguration.TimeToLiveSecs);

                Route route1 = edgeHubConfig.Routes["route1"];
                Assert.NotNull(route1);
                Assert.True(route1.Endpoints.First().GetType() == typeof(CloudEndpoint));
                Route route2 = edgeHubConfig.Routes["route2"];
                Assert.NotNull(route2);
                Endpoint endpoint = route2.Endpoints.First();
                Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                Assert.Equal($"{edgeDeviceId}/module2/input1", endpoint.Id);
                Route route3 = edgeHubConfig.Routes["route3"];
                Assert.NotNull(route3);
                endpoint = route3.Endpoints.First();
                Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                Assert.Equal($"{edgeDeviceId}/module3/input1", endpoint.Id);
                Route route4 = edgeHubConfig.Routes["route4"];
                Assert.NotNull(route4);
                endpoint = route4.Endpoints.First();
                Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                Assert.Equal($"{edgeDeviceId}/module4/input1", endpoint.Id);

                // Make sure reported properties were updated appropriately
                EdgeHubConnection.ReportedProperties reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
                Assert.Equal(200, reportedProperties.LastDesiredStatus.Code);
                Assert.NotNull(reportedProperties.Clients);
                Assert.Equal(0, reportedProperties.Clients.Count);
                Assert.Equal("1.0", reportedProperties.SchemaVersion);
                Assert.Equal(versionInfo, reportedProperties.VersionInfo);

                // Simulate a module and a downstream device that connects to Edge Hub.

                string moduleId = "module1";
                string sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{edgeDeviceId}/modules/{moduleId}");
                string moduleConnectionstring = $"HostName={iothubHostName};DeviceId={edgeDeviceId};ModuleId={moduleId};SharedAccessSignature={sasToken}";
                Try<IIdentity> moduleIdentity = identityFactory.GetWithConnectionString(moduleConnectionstring);
                var moduleProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

                string downstreamDeviceId = "device1";
                sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{downstreamDeviceId}");
                string downstreamDeviceConnectionstring = $"HostName={iothubHostName};DeviceId={downstreamDeviceId};SharedAccessSignature={sasToken}";
                Try<IIdentity> downstreamDeviceIdentity = identityFactory.GetWithConnectionString(downstreamDeviceConnectionstring);
                var downstreamDeviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

                // Connect the module and downstream device and make sure the reported properties are updated as expected.
                await connectionManager.AddDeviceConnection(moduleIdentity.Value, moduleProxy);
                await connectionManager.AddDeviceConnection(downstreamDeviceIdentity.Value, downstreamDeviceProxy);
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
                Assert.Equal("1.0", reportedProperties.SchemaVersion);
                Assert.Equal(versionInfo, reportedProperties.VersionInfo);

                // Update desired propertied and make sure callback is called with valid values
                bool callbackCalled = false;

                Task ConfigUpdatedCallback(EdgeHubConfig updatedConfig)
                {
                    Assert.NotNull(updatedConfig);
                    Assert.NotNull(updatedConfig.StoreAndForwardConfiguration);
                    Assert.NotNull(updatedConfig.Routes);
                    Assert.Equal(4, updatedConfig.Routes.Count);

                    route1 = updatedConfig.Routes["route1"];
                    Assert.NotNull(route1);
                    Assert.True(route1.Endpoints.First().GetType() == typeof(CloudEndpoint));
                    route2 = updatedConfig.Routes["route2"];
                    Assert.NotNull(route2);
                    endpoint = route2.Endpoints.First();
                    Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                    Assert.Equal($"{edgeDeviceId}/module2/input1", endpoint.Id);
                    route3 = updatedConfig.Routes["route4"];
                    Assert.NotNull(route3);
                    endpoint = route3.Endpoints.First();
                    Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                    Assert.Equal($"{edgeDeviceId}/module5/input1", endpoint.Id);
                    route4 = updatedConfig.Routes["route5"];
                    Assert.NotNull(route4);
                    endpoint = route4.Endpoints.First();
                    Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                    Assert.Equal($"{edgeDeviceId}/module6/input1", endpoint.Id);

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
                Assert.Equal("1.0", reportedProperties.SchemaVersion);
                Assert.Equal(versionInfo, reportedProperties.VersionInfo);

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
                Assert.Equal("1.0", reportedProperties.SchemaVersion);
                Assert.Equal(versionInfo, reportedProperties.VersionInfo);

                // If the edge hub restarts, clear out the connected devices in the reported properties.
                await EdgeHubConnection.Create(edgeHubIdentity.Value, twinManager, connectionManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter, versionInfo);
                reportedProperties = await this.GetReportedProperties(registryManager, edgeDeviceId);
                Assert.Null(reportedProperties.Clients);
                Assert.Equal("1.0", reportedProperties.SchemaVersion);
                Assert.Equal(versionInfo, reportedProperties.VersionInfo);

            }
            finally
            {
                try
                {
                    await RegistryManagerHelper.RemoveDevice(edgeDeviceId, registryManager);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
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
            var cc = new ConfigurationContent() { ModuleContent = new Dictionary<string, TwinContent>() };
            var twinContent = new TwinContent();
            cc.ModuleContent["$edgeHub"] = twinContent;

            var desiredProperties = new
            {
                schemaVersion = "1.0",
                routes = new Dictionary<string, string>
                {
                    ["route1"] = "from /* INTO $upstream",
                    ["route2"] = "from /modules/module1 INTO BrokeredEndpoint(\"/modules/module2/inputs/input1\")",
                    ["route3"] = "from /modules/module2 INTO BrokeredEndpoint(\"/modules/module3/inputs/input1\")",
                    ["route4"] = "from /modules/module3 INTO BrokeredEndpoint(\"/modules/module4/inputs/input1\")",
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
            var cc = new ConfigurationContent() { ModuleContent = new Dictionary<string, TwinContent>() };
            var twinContent = new TwinContent();
            cc.ModuleContent["$edgeHub"] = twinContent;

            var desiredProperties = new
            {
                schemaVersion = "1.0",
                routes = new Dictionary<string, string>
                {
                    ["route1"] = "from /* INTO $upstream",
                    ["route2"] = "from /modules/module1 INTO BrokeredEndpoint(\"/modules/module2/inputs/input1\")",
                    ["route4"] = "from /modules/module3 INTO BrokeredEndpoint(\"/modules/module5/inputs/input1\")",
                    ["route5"] = "from /modules/module5 INTO BrokeredEndpoint(\"/modules/module6/inputs/input1\")",
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
