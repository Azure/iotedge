// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class RouterFactory : IRouterFactory
    {
        readonly IEndpointExecutorFactory endpointExecutorFactory;

        public RouterFactory(IEndpointExecutorFactory endpointExecutorFactory)
        {
            this.endpointExecutorFactory = Preconditions.CheckNotNull(endpointExecutorFactory);
        }

        public Task<Router> CreateAsync(string id, string iotHubName)
        {
            return Router.CreateAsync(id, iotHubName, new RouterConfig(new HashSet<Endpoint>(), new HashSet<Route>(), Option.None<Route>()), this.endpointExecutorFactory);
        }

        public Task<Router> CreateAsync(string id, string iotHubName, ISet<Endpoint> endpoints, ISet<Route> routes, Option<Route> fallback)
        {
            return Router.CreateAsync(id, iotHubName, new RouterConfig(endpoints, routes, fallback), this.endpointExecutorFactory);
        }
    }
}
