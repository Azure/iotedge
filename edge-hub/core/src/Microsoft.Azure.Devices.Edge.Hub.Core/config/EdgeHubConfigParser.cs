// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Creates EdgeHubConfig out of EdgeHubDesiredProperties.
    /// </summary>
    public class EdgeHubConfigParser
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubConfigParser>();

        public static EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties desiredProperties, RouteFactory routeFactory)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            ReadOnlyDictionary<string, RouteConfig> routes = ParseRoutes(desiredProperties, routeFactory);

            ValidateAuthorizationPolicy(desiredProperties.Authorizations);

            return new EdgeHubConfig(
                desiredProperties.SchemaVersion,
                routes,
                desiredProperties.StoreAndForwardConfiguration,
                desiredProperties.Authorizations);
        }

        private static ReadOnlyDictionary<string, RouteConfig> ParseRoutes(EdgeHubDesiredProperties desiredProperties, RouteFactory routeFactory)
        {
            var routes = new Dictionary<string, RouteConfig>();
            if (desiredProperties.Routes != null)
            {
                foreach (KeyValuePair<string, RouteConfiguration> inputRoute in desiredProperties.Routes)
                {
                    try
                    {
                        Route route = routeFactory.Create(inputRoute.Value.Route, inputRoute.Value.Priority, inputRoute.Value.TimeToLiveSecs);
                        routes.Add(inputRoute.Key, new RouteConfig(inputRoute.Key, inputRoute.Value.Route, route));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error parsing route {inputRoute.Key} - {ex.Message}", ex);
                    }
                }
            }

            return new ReadOnlyDictionary<string, RouteConfig>(routes);
        }

        private static void ValidateAuthorizationPolicy(AuthorizationConfiguration authorizations)
        {
            if (authorizations != null)
            {
                throw new NotImplementedException();
            }
        }
    }
}
