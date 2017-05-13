// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;

    public class SimpleRouteFactory : IRouteFactory
    {
        readonly IEndpointFactory endpointFactory;
        // This is not being used in the routing code. So hardcode it to fixed value.
        const string IotHubName = "IoTHub";

        public SimpleRouteFactory(IEndpointFactory endpointFactory)
        {
            this.endpointFactory = Preconditions.CheckNotNull(endpointFactory, nameof(endpointFactory));
        }

        public Route Create(string routeString)
        {
            // Parse route into constituents
            ParseRoute(Preconditions.CheckNotNull(routeString, nameof(routeString)), out MessageSource messageSource, out string condition, out string destination);
            Endpoint endpoint = this.endpointFactory.Create(destination);
            var route = new Route(Guid.NewGuid().ToString(), condition, IotHubName, messageSource, new HashSet<Endpoint> { endpoint });
            return route;
        }        

        public IEnumerable<Route> Create(IEnumerable<string> routes)
        {
            return Preconditions.CheckNotNull(routes, nameof(routes))
                .Select(r => this.Create(r));
        }

        // For now, set default values without parsing the route
        static void ParseRoute(string routeString, out MessageSource messageSource, out string condition, out string destination)
        {
            messageSource = MessageSource.Telemetry;            
            condition = "true";
            destination = string.Empty;
        }
    }    
}