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

            ValidateSchemaVersion(desiredProperties.SchemaVersion);

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

        internal static void ValidateSchemaVersion(string schemaVersion)
        {
            if (Core.Constants.ConfigSchemaVersion.CompareMajorVersion(schemaVersion, "desired properties schema") != 0)
            {
                Log.LogWarning(
                    HubCoreEventIds.EdgeHubConfigParser,
                    $"Desired properties schema version {schemaVersion} does not match expected schema version {Core.Constants.ConfigSchemaVersion}. Some settings may not be supported.");
            }
        }
    }
}
