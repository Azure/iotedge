// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Domain object that represents Authorization configuration for Edge Hub Module (MQTT Broker).
    ///
    /// This object is being constructed from the EdgeHub twin's desired properties.
    /// See <see cref="EdgeHubDesiredProperties"/> for DTO.
    /// </summary>
    public class AuthorizationConfig : List<Statement>, IEquatable<AuthorizationConfig>
    {
        public bool Equals(AuthorizationConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Enumerable.SequenceEqual(this, other);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as AuthorizationConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hash;
            }
        }
    }

    public class Statement : IEquatable<Statement>
    {
        [JsonConstructor]
        public Statement(IList<string> identities, IList<Rule> allow, IList<Rule> deny)
        {
            this.Identities = identities;
            this.Allow = allow;
            this.Deny = deny;
        }

        [JsonProperty(PropertyName = "identities")]
        public IList<string> Identities { get; }

        [JsonProperty(PropertyName = "allow")]
        public IList<Rule> Allow { get; }

        [JsonProperty(PropertyName = "deny")]
        public IList<Rule> Deny { get; }

        public bool Equals(Statement other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Enumerable.SequenceEqual(this.Identities, other.Identities)
                && Enumerable.SequenceEqual(this.Allow, other.Allow)
                && Enumerable.SequenceEqual(this.Deny, other.Deny);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as Statement);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Identities.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                hash = this.Allow.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                hash = this.Deny.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hash;
            }
        }
    }

    public class Rule : IEquatable<Rule>
    {
        [JsonConstructor]
        public Rule(IList<string> operations, IList<string> resources)
        {
            this.Operations = operations;
            this.Resources = resources;
        }

        [JsonProperty(PropertyName = "operations")]
        public IList<string> Operations { get; }

        [JsonProperty(PropertyName = "resources")]
        public IList<string> Resources { get; }

        public bool Equals(Rule other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Enumerable.SequenceEqual(this.Operations, other.Operations)
                && Enumerable.SequenceEqual(this.Resources, other.Resources);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as Rule);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Operations.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                hash = this.Resources.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hash;
            }
        }
    }
}
