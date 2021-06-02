// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ManifestIntegrity : IEquatable<ManifestIntegrity>
    {
        [JsonConstructor]
        public ManifestIntegrity(TwinHeader header, TwinSignature signature)
        {
            this.Header = header;
            this.Signature = signature;
        }

        [JsonProperty("header")]
        public TwinHeader Header { get; }
        [JsonProperty("signature")]
        public TwinSignature Signature { get; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ManifestIntegrity);
        }

        public bool Equals(ManifestIntegrity other)
        {
            return other != null &&
                   Equals(this.Header, other.Header) &&
                   Equals(this.Signature, other.Signature);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Header, this.Signature);
        }

        public static bool operator ==(ManifestIntegrity left, ManifestIntegrity right)
        {
            return EqualityComparer<ManifestIntegrity>.Default.Equals(left, right);
        }

        public static bool operator !=(ManifestIntegrity left, ManifestIntegrity right)
        {
            return !(left == right);
        }
    }
}
