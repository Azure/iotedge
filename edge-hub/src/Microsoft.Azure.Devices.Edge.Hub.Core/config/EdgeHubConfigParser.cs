// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    public class EdgeHubConfigParser
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubConfigParser>();

        public static EdgeHubConfig GetEdgeHubConfig(EdgeHubDesiredProperties desiredProperties, RouteFactory routeFactory)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

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

            return new EdgeHubConfig(desiredProperties.SchemaVersion, new ReadOnlyDictionary<string, RouteConfig>(routes), desiredProperties.StoreAndForwardConfiguration);
        }
    }
}
