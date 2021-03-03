// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TwinSignature : IEquatable<TwinSignature>
    {
        [JsonConstructor]
        public TwinSignature(string bytes, string algorithm)
        {
            this.Bytes = bytes;
            this.Algorithm = algorithm;
        }

        [JsonProperty("bytes")]
        public string Bytes { get; }

        [JsonProperty("algorithm")]
        public string Algorithm { get; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TwinSignature);
        }

        public bool Equals(TwinSignature other)
        {
            return other != null &&
                   this.Bytes == other.Bytes &&
                   this.Algorithm == other.Algorithm;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Bytes, this.Algorithm);
        }

        public static bool operator ==(TwinSignature left, TwinSignature right)
        {
            return EqualityComparer<TwinSignature>.Default.Equals(left, right);
        }

        public static bool operator !=(TwinSignature left, TwinSignature right)
        {
            return !(left == right);
        }
    }
}
