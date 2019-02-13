// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using Message = Microsoft.Azure.Devices.Client.Message;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class EdgeHubConnectionTest : IClassFixture<ProtocolHeadFixture>
    {
        const string EdgeHubModuleId = "$edgeHub";

        [Fact]
        public async Task TestEdgeHubConnection()
        {
            const string EdgeDeviceId = "testHubEdgeDevice1";
            var twinMessageConverter = new TwinMessageConverter();
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var messageConverterProvider = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), new DeviceClientMessageConverter() },
                    { typeof(Twin), twinMessageConverter },
                    { typeof(TwinCollection), twinCollectionMessageConverter }
                });

            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string iothubHostName = iotHubConnectionStringBuilder.HostName;
            var identityProvider = new IdentityProvider(iothubHostName);
            var identityFactory = new ClientCredentialsFactory(identityProvider);

            (string edgeDeviceId, string deviceConnStr) = await RegistryManagerHelper.CreateDevice(EdgeDeviceId, iotHubConnectionString, registryManager, true, false);
            string edgeHubConnectionString = $"{deviceConnStr};ModuleId={EdgeHubModuleId}";

            IClientCredentials edgeHubCredentials = identityFactory.GetWithConnectionString(edgeHubConnectionString);
            string sasKey = ConnectionStringHelper.GetSharedAccessKey(deviceConnStr);
            var signatureProvider = new SharedAccessKeySignatureProvider(sasKey);
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var cloudConnectionProvider = new CloudConnectionProvider(
                messageConverterProvider,
                1,
                new ClientProvider(),
                Option.None<UpstreamProtocol>(),
                new ClientTokenProvider(signatureProvider, iothubHostName, edgeDeviceId, TimeSpan.FromMinutes(60)),
                Mock.Of<IDeviceScopeIdentitiesCache>(),
                credentialsCache,
                edgeHubCredentials.Identity,
                TimeSpan.FromMinutes(60),
                true,
                TimeSpan.FromSeconds(20),
                Option.None<IWebProxy>());
            var connectionManager = new ConnectionManager(cloudConnectionProvider, Mock.Of<ICredentialsCache>(), identityProvider);

            try
            {
                Mock.Get(credentialsCache)
                    .Setup(c => c.Get(edgeHubCredentials.Identity))
                    .ReturnsAsync(Option.Some(edgeHubCredentials));
                Assert.NotNull(edgeHubCredentials);
                Assert.NotNull(edgeHubCredentials.Identity);

                // Set Edge hub desired properties
                await this.SetDesiredProperties(registryManager, edgeDeviceId);

                var endpointFactory = new EndpointFactory(connectionManager, new RoutingMessageConverter(), edgeDeviceId);
                var routeFactory = new EdgeRouteFactory(endpointFactory);

                var dbStoreProvider = new InMemoryDbStoreProvider();
                IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
                IEntityStore<string, TwinInfo> twinStore = storeProvider.GetEntityStore<string, TwinInfo>("twins");
                var twinManager = new TwinManager(connectionManager, twinCollectionMessageConverter, twinMessageConverter, Option.Some(twinStore));
                var routerConfig = new RouterConfig(Enumerable.Empty<Route>());
                TimeSpan defaultTimeout = TimeSpan.FromSeconds(60);
                var endpointExecutorFactory = new SyncEndpointExecutorFactory(new EndpointExecutorConfig(defaultTimeout, new FixedInterval(0, TimeSpan.FromSeconds(1)), defaultTimeout, true));
                Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), iothubHostName, routerConfig, endpointExecutorFactory);
                IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);
                var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();
                var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);
                IEdgeHub edgeHub = new RoutingEdgeHub(router, new RoutingMessageConverter(), connectionManager, twinManager, edgeDeviceId, invokeMethodHandler, subscriptionProcessor);
                cloudConnectionProvider.BindEdgeHub(edgeHub);

                var versionInfo = new VersionInfo("v1", "b1", "c1");

                // Create Edge Hub connection
                EdgeHubConnection edgeHubConnection = await EdgeHubConnection.Create(
                    edgeHubCredentials.Identity,
                    edgeHub,
                    twinManager,
                    connectionManager,
                    routeFactory,
                    twinCollectionMessageConverter,
                    twinMessageConverter,
                    versionInfo,
                    new NullDeviceScopeIdentitiesCache());
                await Task.Delay(TimeSpan.FromMinutes(1));

                // Get and Validate EdgeHubConfig
                Option<EdgeHubConfig> edgeHubConfigOption = await edgeHubConnection.GetConfig();
                Assert.True(edgeHubConfigOption.HasValue);
                EdgeHubConfig edgeHubConfig = edgeHubConfigOption.OrDefault();
                Assert.Equal("1.0", edgeHubConfig.SchemaVersion);
                Assert.NotNull(edgeHubConfig.Routes);
                Assert.NotNull(edgeHubConfig.StoreAndForwardConfiguration);
                Assert.Equal(20, edgeHubConfig.StoreAndForwardConfiguration.TimeToLiveSecs);

                List<(string Name, string Value, Route Route)> routes = edgeHubConfig.Routes.ToList();
                Assert.Equal(4, routes.Count);

                (string Name, string Value, Route Route) route1 = routes[0];
                Assert.NotNull(route1);
                Assert.True(route1.Route.Endpoints.First().GetType() == typeof(CloudEndpoint));
                Assert.Equal("route1", route1.Name);
                Assert.Equal("from /* INTO $upstream", route1.Value);

                (string Name, string Value, Route Route) route2 = routes[1];
                Assert.NotNull(route2);
                Endpoint endpoint = route2.Route.Endpoints.First();
                Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                Assert.Equal($"{edgeDeviceId}/module2/input1", endpoint.Id);
                Assert.Equal("route2", route2.Name);
                Assert.Equal("from /modules/module1 INTO BrokeredEndpoint(\"/modules/module2/inputs/input1\")", route2.Value);

                (string Name, string Value, Route Route) route3 = routes[2];
                Assert.NotNull(route3);
                endpoint = route3.Route.Endpoints.First();
                Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                Assert.Equal($"{edgeDeviceId}/module3/input1", endpoint.Id);
                Assert.Equal("route3", route3.Name);
                Assert.Equal("from /modules/module2 INTO BrokeredEndpoint(\"/modules/module3/inputs/input1\")", route3.Value);

                (string Name, string Value, Route Route) route4 = routes[3];
                Assert.NotNull(route4);
                endpoint = route4.Route.Endpoints.First();
                Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                Assert.Equal($"{edgeDeviceId}/module4/input1", endpoint.Id);
                Assert.Equal("route4", route4.Name);
                Assert.Equal("from /modules/module3 INTO BrokeredEndpoint(\"/modules/module4/inputs/input1\")", route4.Value);

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
                IClientCredentials moduleClientCredentials = identityFactory.GetWithConnectionString(moduleConnectionstring);
                var moduleProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

                string downstreamDeviceId = "device1";
                sasToken = TokenHelper.CreateSasToken($"{iothubHostName}/devices/{downstreamDeviceId}");
                string downstreamDeviceConnectionstring = $"HostName={iothubHostName};DeviceId={downstreamDeviceId};SharedAccessSignature={sasToken}";
                IClientCredentials downstreamDeviceCredentials = identityFactory.GetWithConnectionString(downstreamDeviceConnectionstring);
                var downstreamDeviceProxy = Mock.Of<IDeviceProxy>(d => d.IsActive);

                // Connect the module and downstream device and make sure the reported properties are updated as expected.
                await connectionManager.AddDeviceConnection(moduleClientCredentials.Identity, moduleProxy);
                await connectionManager.AddDeviceConnection(downstreamDeviceCredentials.Identity, downstreamDeviceProxy);
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

                    routes = updatedConfig.Routes.ToList();
                    Assert.Equal(4, routes.Count);

                    route1 = routes[0];
                    Assert.NotNull(route1);
                    Assert.True(route1.Route.Endpoints.First().GetType() == typeof(CloudEndpoint));
                    Assert.Equal("route1", route1.Name);
                    Assert.Equal("from /* INTO $upstream", route1.Value);

                    route2 = routes[1];
                    Assert.NotNull(route2);
                    endpoint = route2.Route.Endpoints.First();
                    Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                    Assert.Equal($"{edgeDeviceId}/module2/input1", endpoint.Id);
                    Assert.Equal("route2", route2.Name);
                    Assert.Equal("from /modules/module1 INTO BrokeredEndpoint(\"/modules/module2/inputs/input1\")", route2.Value);

                    route3 = routes[2];
                    Assert.NotNull(route3);
                    endpoint = route3.Route.Endpoints.First();
                    Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                    Assert.Equal($"{edgeDeviceId}/module5/input1", endpoint.Id);
                    Assert.Equal("route4", route3.Name);
                    Assert.Equal("from /modules/module3 INTO BrokeredEndpoint(\"/modules/module5/inputs/input1\")", route3.Value);

                    route4 = routes[3];
                    Assert.NotNull(route4);
                    endpoint = route4.Route.Endpoints.First();
                    Assert.True(endpoint.GetType() == typeof(ModuleEndpoint));
                    Assert.Equal($"{edgeDeviceId}/module6/input1", endpoint.Id);
                    Assert.Equal("route5", route4.Name);
                    Assert.Equal("from /modules/module5 INTO BrokeredEndpoint(\"/modules/module6/inputs/input1\")", route4.Value);

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
                await EdgeHubConnection.Create(
                    edgeHubCredentials.Identity,
                    edgeHub,
                    twinManager,
                    connectionManager,
                    routeFactory,
                    twinCollectionMessageConverter,
                    twinMessageConverter,
                    versionInfo,
                    new NullDeviceScopeIdentitiesCache());
                await Task.Delay(TimeSpan.FromMinutes(1));
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

            var cc = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeHub"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = desiredProperties
                    },
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = new object()
                    }
                }
            };

            await registryManager.ApplyConfigurationContentOnDeviceAsync(edgeDeviceId, cc);
        }

        async Task UpdateDesiredProperties(RegistryManager registryManager, string edgeDeviceId)
        {
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

            var cc = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>
                {
                    ["$edgeHub"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = desiredProperties
                    },
                    ["$edgeAgent"] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = new object()
                    }
                }
            };

            await registryManager.ApplyConfigurationContentOnDeviceAsync(edgeDeviceId, cc);
        }
    }
}
