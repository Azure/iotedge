// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Domain object that represents MQTT Broker configuration for Edge Hub Module.
    ///
    /// This object is being constructed from the EdgeHub twin's desired properties.
    /// See <see cref="BrokerProperties"/> for DTO.
    /// </summary>
    public class BrokerConfig : IEquatable<BrokerConfig>
    {
        public BrokerConfig()
        {
        }

        public BrokerConfig(Option<BridgeConfig> bridges, Option<AuthorizationConfig> authorizations)
        {
            this.Bridges = bridges;
            this.Authorizations = authorizations;
        }

        public Option<BridgeConfig> Bridges { get; }

        public Option<AuthorizationConfig> Authorizations { get; }

        public bool Equals(BrokerConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Bridges.Equals(other.Bridges)
                && this.Authorizations.Equals(other.Authorizations);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as BrokerConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Bridges
                        .Map(config => config.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode())))
                        .GetOrElse(0);
                hash = this.Authorizations
                        .Map(config => config.Statements.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode())))
                        .GetOrElse(0);
                return hash;
            }
        }
    }
}
