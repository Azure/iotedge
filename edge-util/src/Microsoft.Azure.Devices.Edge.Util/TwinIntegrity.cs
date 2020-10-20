// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TwinIntegrity : IEquatable<TwinIntegrity>
    {
        [JsonConstructor]
        public TwinIntegrity(TwinHeader header, TwinSignature signature)
        {
            this.Header = header;
            this.Signature = signature;
        }

        public TwinHeader Header { get; }
        public TwinSignature Signature { get; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TwinIntegrity);
        }

        public bool Equals(TwinIntegrity other)
        {
            return other != null &&
                   EqualityComparer<TwinHeader>.Default.Equals(this.Header, other.Header) &&
                   EqualityComparer<TwinSignature>.Default.Equals(this.Signature, other.Signature);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Header, this.Signature);
        }

        public static bool operator ==(TwinIntegrity left, TwinIntegrity right)
        {
            return EqualityComparer<TwinIntegrity>.Default.Equals(left, right);
        }

        public static bool operator !=(TwinIntegrity left, TwinIntegrity right)
        {
            return !(left == right);
        }
    }
}
