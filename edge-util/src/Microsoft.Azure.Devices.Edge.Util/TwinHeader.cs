// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TwinHeader : IEquatable<TwinHeader>
    {
        [JsonConstructor]
        public TwinHeader(string version, string cert1, string cert2)
        {
            this.Version = version;
            this.Cert1 = cert1;
            this.Cert2 = cert2;
        }

        [JsonProperty("version")]
        public string Version { get; }

        [JsonProperty("cert1")]
        public string Cert1 { get; }

        [JsonProperty("cert2")]
        public string Cert2 { get; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TwinHeader);
        }

        public bool Equals(TwinHeader other)
        {
            return other != null &&
                   this.Version == other.Version &&
                   this.Cert1 == other.Cert1 &&
                   this.Cert2 == other.Cert2;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Version, this.Cert1, this.Cert2);
        }

        public static bool operator ==(TwinHeader left, TwinHeader right)
        {
            return EqualityComparer<TwinHeader>.Default.Equals(left, right);
        }

        public static bool operator !=(TwinHeader left, TwinHeader right)
        {
            return !(left == right);
        }
    }
}
