// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TwinHeader : IEquatable<TwinHeader>
    {
        [JsonConstructor]
        public TwinHeader(string[] signercert, string[] intermediatecacert)
        {
            this.SignerCert = signercert;
            this.IntermediateCert = Option.Maybe(intermediatecacert);
        }

        [JsonProperty("signercert")]
        public string[] SignerCert { get; }

        [JsonProperty("intermediatecacert")]
        public Option<string[]> IntermediateCert { get; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TwinHeader);
        }

        public bool Equals(TwinHeader other)
        {
            return other != null &&
                   this.SignerCert == other.SignerCert &&
                   this.IntermediateCert == other.IntermediateCert;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.SignerCert, this.IntermediateCert);
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
