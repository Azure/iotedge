// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;

    public class LocalConfigSource : IConfigSource
    {
        readonly EdgeHubConfig edgeHubConfig;

        public LocalConfigSource(RouteFactory routeFactory, IDictionary<string, string> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));
            Preconditions.CheckNotNull(routes, nameof(routes));
            Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            IDictionary<string, Route> parsedRoutes = routes.Select(r => (r.Key, routeFactory.Create(r.Value)))
                .ToDictionary(r => r.Item1, r=> r.Item2);
            this.edgeHubConfig = new EdgeHubConfig(Core.Constants.ConfigSchemaVersion, parsedRoutes, storeAndForwardConfiguration);
        }

        public Task<EdgeHubConfig> GetConfig() => Task.FromResult(this.edgeHubConfig);

        public void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback) { }
    }
}
