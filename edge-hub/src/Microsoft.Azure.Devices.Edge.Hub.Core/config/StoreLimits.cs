// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class StoreLimits : IEquatable<StoreLimits>
    {
        [JsonConstructor]
        public StoreLimits(long maxSize)
            : this(maxSize, Option.None<int>())
        {
        }

        public StoreLimits(long maxSizeBytes, Option<int> checkFrequency)
        {
            this.MaxSizeBytes = maxSizeBytes;
            this.CheckFrequencySecs = checkFrequency;
        }

        [JsonProperty(PropertyName = "maxSizeBytes")]
        public long MaxSizeBytes { get; }

        [JsonProperty(PropertyName = "checkFrequencySecs")]
        [JsonConverter(typeof(OptionConverter<long>), true)]
        public Option<int> CheckFrequencySecs { get; }

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

            return this.MaxSizeBytes == other.MaxSizeBytes &&
                this.CheckFrequencySecs.Equals(other.CheckFrequencySecs);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as StoreLimits);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.MaxSizeBytes.GetHashCode();
                hashCode = (hashCode * 397) ^ this.CheckFrequencySecs.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(StoreLimits left, StoreLimits right) => Equals(left, right);

        public static bool operator !=(StoreLimits left, StoreLimits right) => !Equals(left, right);
    }
}
