// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class AgentDemandSet : IEquatable<AgentDemandSet>
    {
        readonly SortedSet<AgentCapability> capabilities;
        readonly string name;

        public AgentDemandSet(string name, HashSet<AgentCapability> capabilities)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNulOrEmptySet(capabilities, nameof(capabilities));

            this.name = name;
            this.capabilities = new SortedSet<AgentCapability>(capabilities);
        }

        public string Name => this.name;

        public ImmutableSortedSet<AgentCapability> Capabilities => this.capabilities.ToImmutableSortedSet();

        public bool Equals(AgentDemandSet other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.name, other.name, StringComparison.OrdinalIgnoreCase) && this.capabilities.SetEquals(other.capabilities);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((AgentDemandSet)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.capabilities.First(), this.name);
        }
    }
}
