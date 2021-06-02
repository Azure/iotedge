// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using Newtonsoft.Json;

    public class StoreLimits : IEquatable<StoreLimits>
    {
        [JsonConstructor]
        public StoreLimits(long maxSizeBytes)
        {
            this.MaxSizeBytes = maxSizeBytes;
        }

        [JsonProperty(PropertyName = "maxSizeBytes")]
        public long MaxSizeBytes { get; }

        public bool Equals(StoreLimits other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.MaxSizeBytes == other.MaxSizeBytes;
        }

        public override bool Equals(object obj)
            => this.Equals(obj as StoreLimits);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.MaxSizeBytes.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(StoreLimits left, StoreLimits right) => Equals(left, right);

        public static bool operator !=(StoreLimits left, StoreLimits right) => !Equals(left, right);
    }
}
