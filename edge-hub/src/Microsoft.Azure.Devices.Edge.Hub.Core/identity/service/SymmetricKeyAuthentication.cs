// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class SymmetricKeyAuthentication : IEquatable<SymmetricKeyAuthentication>

    {
        [JsonConstructor]
        public SymmetricKeyAuthentication(string primaryKey, string secondaryKey)
        {
            this.PrimaryKey = Preconditions.CheckNonWhiteSpace(primaryKey, nameof(primaryKey));
            this.SecondaryKey = Preconditions.CheckNonWhiteSpace(secondaryKey, nameof(secondaryKey));
        }

        [JsonProperty("primaryKey")]
        public string PrimaryKey { get; }

        [JsonProperty("secondaryKey")]
        public string SecondaryKey { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((SymmetricKeyAuthentication)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.PrimaryKey != null ? this.PrimaryKey.GetHashCode() : 0) * 397) ^ (this.SecondaryKey != null ? this.SecondaryKey.GetHashCode() : 0);
            }
        }

        public bool Equals(SymmetricKeyAuthentication other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.PrimaryKey, other.PrimaryKey) && string.Equals(this.SecondaryKey, other.SecondaryKey);
        }
    }
}
