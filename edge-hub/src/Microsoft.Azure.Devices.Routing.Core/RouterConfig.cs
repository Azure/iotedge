// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    using Microsoft.Azure.Devices.Routing.Core.Util;

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
            : this(routesList.SelectMany(r => r.Endpoints), routesList)
        {
        }

        public ISet<Endpoint> Endpoints { get; }

        public Option<Route> Fallback { get; }

        public ISet<Route> Routes { get; }
    }
}
