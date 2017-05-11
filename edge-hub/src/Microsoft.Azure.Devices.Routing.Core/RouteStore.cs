// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class RouteStore : IRouteStore
    {
        readonly ImmutableDictionary<string, IEnumerable<Endpoint>> endpoints;
        readonly ImmutableDictionary<string, IEnumerable<Route>> routes;
        static readonly IEnumerable<Endpoint> EmptyEndpoints = new List<Endpoint>();
        static readonly IEnumerable<Route> EmptyRoutes = new List<Route>();

        public RouteStore()
            : this(ImmutableDictionary<string, RouterConfig>.Empty)
        {
        }

        public RouteStore(IDictionary<string, RouterConfig> configs)
        {
            this.endpoints = configs.ToImmutableDictionary(key => key.Key, value => value.Value.Endpoints.ToList() as IEnumerable<Endpoint>);
            this.routes = configs.ToImmutableDictionary(key => key.Key, value => value.Value.Routes.ToList() as IEnumerable<Route>);
        }

        public Task<RouterConfig> GetRouterConfigAsync(string iotHubName, CancellationToken token) =>
            Task.FromResult(new RouterConfig(
                this.endpoints.GetOrElse(iotHubName, EmptyEndpoints), 
                this.routes.GetOrElse(iotHubName, EmptyRoutes),
                Option.None<Route>()
                )
            );
    }
}