// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class EdgeHubDesiredProperties
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties(string schemaVersion, IDictionary<string, string> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = schemaVersion;
            this.Routes = routes;
            this.StoreAndForwardConfiguration = storeAndForwardConfiguration;
        }

        public string SchemaVersion { get; }

        public IDictionary<string, string> Routes { get; }

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
