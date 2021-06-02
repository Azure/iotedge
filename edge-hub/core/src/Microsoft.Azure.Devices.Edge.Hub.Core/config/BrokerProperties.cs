// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using Newtonsoft.Json;

    /// <summary>
    /// DTO that is used to deserialize MQTT Broker Config from EdgeHub twin
    /// into <see cref="BrokerConfig" />.
    /// </summary>
    public class BrokerProperties
    {
        [JsonConstructor]
        public BrokerProperties(BridgeConfig bridges, AuthorizationProperties authorizations)
        {
            this.Bridges = bridges ?? new BridgeConfig();
            this.Authorizations = authorizations ?? new AuthorizationProperties();
        }

        [JsonProperty(PropertyName = "bridges")]
        public BridgeConfig Bridges { get; }

        [JsonProperty(PropertyName = "authorizations")]
        public AuthorizationProperties Authorizations { get; }
    }
}
