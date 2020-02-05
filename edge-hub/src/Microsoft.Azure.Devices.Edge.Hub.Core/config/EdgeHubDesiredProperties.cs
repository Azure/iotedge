// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class EdgeHubDesiredProperties
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties(string schemaVersion, IDictionary<string, RouteConfiguration> routes, IDictionary<string, string> legacyRoutes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = schemaVersion;
            this.Routes = routes;
            this.LegacyRoutes = legacyRoutes;
            this.StoreAndForwardConfiguration = storeAndForwardConfiguration;
        }

        public string SchemaVersion { get; }

        public IDictionary<string, RouteConfiguration> Routes;

        public IDictionary<string, string> LegacyRoutes { get; }

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
