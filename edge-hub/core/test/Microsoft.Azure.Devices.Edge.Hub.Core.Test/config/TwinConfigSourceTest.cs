// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class TwinConfigSourceTest
    {
        static TwinCollection GetTwinDesiredProperties(string schemaVersion, object integrity)
        {
            var desiredProperties = new
            {
                routes = new
                {
                    route = "FROM /messages/* INTO $upstream"
                },
                schemaVersion,
                storeAndForwardConfiguration = new
                {
                    timeToLiveSecs = 7200
                },
                integrity,
                version = "10"
            };
            JsonSerializerSettings settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            return new TwinCollection(JsonConvert.SerializeObject(desiredProperties, settings));
        }

        public static TwinConfigSource GetTwinConfigSource(Option<X509Certificate2> manifestTrustBundle)
        {
            var connectionManager = new ConnectionManager(Mock.Of<ICloudConnectionProvider>(), Mock.Of<ICredentialsCache>(), Mock.Of<IIdentityProvider>(), Mock.Of<IDeviceConnectivityManager>());
            var endpointFactory = new EndpointFactory(connectionManager, new RoutingMessageConverter(), "testHubEdgeDevice1", 10, 10, false);
            var routeFactory = new EdgeRouteFactory(endpointFactory);
            var configParser = new EdgeHubConfigParser(routeFactory, new BrokerPropertiesValidator());
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var twinMessageConverter = new TwinMessageConverter();
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            IEntityStore<string, TwinInfo> twinStore = storeProvider.GetEntityStore<string, TwinInfo>("twins");
            var twinManager = new TwinManager(connectionManager, twinCollectionMessageConverter, twinMessageConverter, Option.Some(twinStore));
            var versionInfo = new VersionInfo(string.Empty, string.Empty, string.Empty);

            // Create Edge Hub connection
            EdgeHubConnection edgeHubConnection = GetEdgeHubConnection().Result;

            // TwinConfig Source
            return new TwinConfigSource(
                edgeHubConnection,
                string.Empty,
                versionInfo,
                twinManager,
                twinMessageConverter,
                twinCollectionMessageConverter,
                configParser,
                manifestTrustBundle);
        }

        public static async Task<EdgeHubConnection> GetEdgeHubConnection()
        {
            var connectionManager = new ConnectionManager(Mock.Of<ICloudConnectionProvider>(), Mock.Of<ICredentialsCache>(), Mock.Of<IIdentityProvider>(), Mock.Of<IDeviceConnectivityManager>());
            var endpointFactory = new EndpointFactory(connectionManager, new RoutingMessageConverter(), "testHubEdgeDevice1", 10, 10, false);
            var routeFactory = new EdgeRouteFactory(endpointFactory);
            var twinCollectionMessageConverter = new TwinCollectionMessageConverter();
            var twinMessageConverter = new TwinMessageConverter();
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            IEntityStore<string, TwinInfo> twinStore = storeProvider.GetEntityStore<string, TwinInfo>("twins");
            var twinManager = new TwinManager(connectionManager, twinCollectionMessageConverter, twinMessageConverter, Option.Some(twinStore));
            var versionInfo = new VersionInfo(string.Empty, string.Empty, string.Empty);

            return await EdgeHubConnection.Create(
            Mock.Of<IIdentity>(i => i.Id == "someid"),
            Mock.Of<IEdgeHub>(),
            twinManager,
            Mock.Of<IConnectionManager>(),
            routeFactory,
            twinCollectionMessageConverter,
            versionInfo,
            new NullDeviceScopeIdentitiesCache());
        }
    }
}
