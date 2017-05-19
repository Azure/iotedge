// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Runtime.Serialization;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PortBindingType
    {
        [EnumMember(Value = "tcp")]
        Tcp,
        [EnumMember(Value = "udp")]
        Udp
    }

    public class PortBinding : IEquatable<PortBinding>
    {
        public string From { get; }

        public string To { get; }

        public PortBindingType Type { get; }

        public PortBinding(string to, string from)
            : this(to, from, PortBindingType.Tcp)
        {
        }

        [JsonConstructor]
        public PortBinding(string to, string from, PortBindingType type)
        {
            this.To = Preconditions.CheckNotNull(to, nameof(to));
            this.From = Preconditions.CheckNotNull(from, nameof(from));
            this.Type = type;
        }

        public override bool Equals(object obj) => this.Equals(obj as PortBinding);

        public bool Equals(PortBinding other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.From, other.From) &&
                string.Equals(this.To, other.To) &&
                this.Type == other.Type;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.From != null ? this.From.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.To != null ? this.To.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.Type;
                return hashCode;
            }
        }
    }
}