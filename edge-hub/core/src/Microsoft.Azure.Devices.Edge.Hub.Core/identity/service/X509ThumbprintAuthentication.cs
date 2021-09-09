// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class X509ThumbprintAuthentication : IEquatable<X509ThumbprintAuthentication>
    {
        [JsonConstructor]
        public X509ThumbprintAuthentication(string primaryThumbprint, string secondaryThumbprint)
        {
            this.PrimaryThumbprint = Preconditions.CheckNonWhiteSpace(primaryThumbprint, nameof(primaryThumbprint));
            this.SecondaryThumbprint = Preconditions.CheckNonWhiteSpace(secondaryThumbprint, nameof(secondaryThumbprint));
        }

        [JsonProperty("primaryThumbprint")]
        public string PrimaryThumbprint { get; }

        [JsonProperty("secondaryThumbprint")]
        public string SecondaryThumbprint { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((X509ThumbprintAuthentication)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.PrimaryThumbprint != null ? this.PrimaryThumbprint.GetHashCode() : 0) * 397) ^ (this.SecondaryThumbprint != null ? this.SecondaryThumbprint.GetHashCode() : 0);
            }
        }

        public bool Equals(X509ThumbprintAuthentication other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.PrimaryThumbprint, other.PrimaryThumbprint) && string.Equals(this.SecondaryThumbprint, other.SecondaryThumbprint);
        }
    }
}
