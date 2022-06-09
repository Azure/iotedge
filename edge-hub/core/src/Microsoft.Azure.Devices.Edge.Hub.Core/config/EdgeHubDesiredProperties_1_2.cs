// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// DTO that is used to deserialize EdgeHub Desired properties for schema v1.2
    /// of the twin into <see cref="EdgeHubConfig" /> by <see cref="TwinConfigSource" />.
    /// </summary>
    public class EdgeHubDesiredProperties_1_2
    {
        [JsonConstructor]
        public EdgeHubDesiredProperties_1_2(
            string schemaVersion,
            IDictionary<string, RouteSpec> routes,
            StoreAndForwardConfiguration storeAndForwardConfiguration,
            BrokerProperties brokerConfiguration)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.StoreAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            // can be null for old versions.
            this.BrokerConfiguration = brokerConfiguration;
        }

        [JsonProperty(PropertyName = Constants.SchemaVersionKey)]
        public string SchemaVersion { get; }

        [JsonConverter(typeof(RouteSpecConverter))]
        public IDictionary<string, RouteSpec> Routes;

        [JsonProperty(PropertyName = "storeAndForwardConfiguration")]
        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }

        [JsonProperty(PropertyName = "mqttBroker")]
        public BrokerProperties BrokerConfiguration { get; }
    }
}
