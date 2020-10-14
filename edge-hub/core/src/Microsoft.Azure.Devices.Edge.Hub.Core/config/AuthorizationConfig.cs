// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Domain object that represents Authorization configuration for Edge Hub Module (MQTT Broker).
    ///
    /// This object is being constructed from the EdgeHub twin's desired properties.
    /// See <see cref="AuthorizationProperties"/> for DTO.
    /// </summary>
    public class AuthorizationConfig : IEquatable<AuthorizationConfig>
    {
        public IList<Statement> Statements { get; }

        public AuthorizationConfig(IList<Statement> statements)
        {
            this.Statements = Preconditions.CheckNotNull(statements, nameof(statements));
        }

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

            return Enumerable.SequenceEqual(this.Statements, other.Statements);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as AuthorizationConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Statements.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hash;
            }
        }
    }

    public class Statement : IEquatable<Statement>
    {
        public Statement(
            Effect effect,
            IList<string> identities,
            IList<string> operations,
            IList<string> resources)
        {
            this.Effect = effect;
            this.Identities = identities;
            this.Operations = operations;
            this.Resources = resources;
        }

        public Effect Effect { get; }

        public IList<string> Identities { get; }

        public IList<string> Operations { get; }

        public IList<string> Resources { get; }

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
                && Enumerable.SequenceEqual(this.Operations, other.Operations)
                && Enumerable.SequenceEqual(this.Resources, other.Resources);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as Statement);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = this.Identities.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                hash = this.Operations.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                hash = this.Resources.Aggregate(hash, (acc, item) => (acc * 31 + item.GetHashCode()));
                return hash;
            }
        }
    }

    public enum Effect
    {
        Allow = 0,
        Deny = 1,
    }
}
