// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;

    public class LocalConfigSource : IConfigSource
    {
        readonly EdgeHubConfig edgeHubConfig;

        public LocalConfigSource(RouteFactory routeFactory, IDictionary<string, string> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));
            Preconditions.CheckNotNull(routes, nameof(routes));
            Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            IDictionary<string, RouteConfig> parsedRoutes = routes.ToDictionary(r => r.Key, r => new RouteConfig(r.Key, r.Value, routeFactory.Create(r.Value)));
            this.edgeHubConfig = new EdgeHubConfig(Constants.ConfigSchemaVersion.ToString(), new ReadOnlyDictionary<string, RouteConfig>(parsedRoutes), storeAndForwardConfiguration);
        }

        public Task<Option<EdgeHubConfig>> GetCachedConfig() => Task.FromResult(Option.Some(this.edgeHubConfig));

        public Task<Option<EdgeHubConfig>> GetConfig() => Task.FromResult(Option.Some(this.edgeHubConfig));

        public void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback)
        {
        }
    }
}
