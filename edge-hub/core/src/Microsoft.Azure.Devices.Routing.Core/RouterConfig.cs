// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RouterConfig
    {
        public RouterConfig(IEnumerable<Route> routes)
            : this(routes.ToList())
        {
        }

        public RouterConfig(IEnumerable<Endpoint> endpoints, IEnumerable<Route> routes)
            : this(endpoints, routes, Option.None<Route>())
        {
        }

        public RouterConfig(IEnumerable<Endpoint> endpoints, IEnumerable<Route> routes, Option<Route> fallback)
        {
            this.Endpoints = Preconditions.CheckNotNull(endpoints).ToImmutableHashSet();
            this.Routes = Preconditions.CheckNotNull(routes).ToImmutableHashSet();
            this.Fallback = Preconditions.CheckNotNull(fallback);
        }

        RouterConfig(IList<Route> routesList)
            : this(routesList.Select(r => r.Endpoint), routesList)
        {
        }

        public ISet<Endpoint> Endpoints { get; }

        public Option<Route> Fallback { get; }

        public ISet<Route> Routes { get; }
    }
}
