// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// DTO that is used to deserialize EdgeHub Desired properties for schema v1.0
    /// of the twin into <see cref="EdgeHubConfig" /> by <see cref="TwinConfigSource" />.
    /// </summary>
    public class EdgeHubDesiredProperties_1_0
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties_1_0(
            string schemaVersion,
            IDictionary<string, string> routes,
            StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = schemaVersion;
            this.Routes = routes;
            this.StoreAndForwardConfiguration = storeAndForwardConfiguration;
        }

        [JsonProperty(PropertyName = Constants.SchemaVersionKey)]
        public string SchemaVersion { get; }

        public IDictionary<string, string> Routes { get; }

        [JsonProperty(PropertyName = "storeAndForwardConfiguration")]
        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
