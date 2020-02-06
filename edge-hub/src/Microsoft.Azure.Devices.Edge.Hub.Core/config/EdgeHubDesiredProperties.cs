// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class EdgeHubDesiredProperties
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties(string schemaVersion, IDictionary<string, RouteConfiguration> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = schemaVersion;
            this.Routes = routes;
            this.StoreAndForwardConfiguration = storeAndForwardConfiguration;
        }

        public string SchemaVersion { get; }

        [JsonConverter(typeof(RouteConfigurationDictionaryConverter))]
        public IDictionary<string, RouteConfiguration> Routes;

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
