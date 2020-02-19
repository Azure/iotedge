// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeHubDesiredProperties
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties(string schemaVersion, IDictionary<string, RouteConfiguration> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.StoreAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
        }

        public string SchemaVersion { get; }

        [JsonConverter(typeof(RouteConfigurationConverter))]
        public IDictionary<string, RouteConfiguration> Routes;

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
