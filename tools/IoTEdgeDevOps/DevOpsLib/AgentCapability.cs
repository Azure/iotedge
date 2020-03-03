// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;

    public class AgentCapability : IEquatable<AgentCapability>, IComparable<AgentCapability>, ICloneable
    {
        readonly string name;
        readonly string value;

        public AgentCapability(string name, string value)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(name, nameof(name));
            ValidationUtil.ThrowIfNull(value, nameof(value));

            // vsts agent capability is case-insensitive
            this.name = name;
            this.value = value;
        }

        public string Name => this.name;

        public string Value => this.value;

        public object Clone()
        {
            return new AgentCapability(this.Name, this.Value);
        }

        public bool Equals(AgentCapability other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.name, other.name, StringComparison.OrdinalIgnoreCase) && string.Equals(this.value, other.value, StringComparison.OrdinalIgnoreCase);
        }

        public int CompareTo(AgentCapability other)
        {
            var result = string.Compare(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            return string.Compare(this.Value, other.Value, StringComparison.OrdinalIgnoreCase);
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

            return this.Equals((AgentCapability)obj);
        }

        public override int GetHashCode()
        {
            return this.name.ToLower().GetHashCode();
        }

        public override string ToString()
        {
            return $"{this.name}={this.value}";
        }
    }
}
