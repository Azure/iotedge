// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// DTO that is used to deserialize EdgeHub Desired properties for schema v1.1
    /// of the twin into <see cref="EdgeHubConfig" /> by <see cref="TwinConfigSource" />.
    /// </summary>
    public class EdgeHubDesiredProperties_1_1
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties_1_1(
            string schemaVersion,
            IDictionary<string, RouteSpec> routes,
            StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.StoreAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
        }

        [JsonProperty(PropertyName = Constants.SchemaVersionKey)]
        public string SchemaVersion { get; }

        [JsonConverter(typeof(RouteSpecConverter))]
        public IDictionary<string, RouteSpec> Routes;

        [JsonProperty(PropertyName = "storeAndForwardConfiguration")]
        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
