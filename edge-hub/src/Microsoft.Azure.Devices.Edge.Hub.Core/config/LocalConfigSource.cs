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
            IEnumerable<(string Name, string Value, Route Route)> parsedRoutes = routes.Select(r => (r.Key, r.Value, routeFactory.Create(r.Value)));
            this.edgeHubConfig = new EdgeHubConfig(Core.Constants.ConfigSchemaVersion.ToString(), parsedRoutes, storeAndForwardConfiguration);
        }

        public Task<Option<EdgeHubConfig>> GetConfig() => Task.FromResult(Option.Some(this.edgeHubConfig));

        public void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback) { }
    }
}
