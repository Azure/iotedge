// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// DTO that is used to deserialize MQTT Broker Config from EdgeHub twin
    /// into <see cref="BrokerConfig" />.
    /// </summary>
    public class BrokerProperties
    {
        public BrokerProperties()
        {
        }

        [JsonConstructor]
        public BrokerProperties(BridgeConfig bridges, AuthorizationProperties authorizations)
        {
            this.Bridges = bridges;
            this.Authorizations = authorizations;
        }

        [JsonProperty(PropertyName = "bridges")]
        public BridgeConfig Bridges { get; }

        [JsonProperty(PropertyName = "authorizations")]
        public AuthorizationProperties Authorizations { get; }
    }
}
