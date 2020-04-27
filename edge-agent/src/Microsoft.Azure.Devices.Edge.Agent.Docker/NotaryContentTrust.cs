// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.ComponentModel;
    using Newtonsoft.Json;

    public class NotaryContentTrust : IEquatable<NotaryContentTrust>
    {
        [JsonProperty("rootJsonPath", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string RootJsonPath { get; set; }

        [JsonProperty("rootID", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string RootID { get; set; }

        [JsonProperty("rootCertificatePath", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string RootCertificatePath { get; set; }

        [JsonProperty("disableTOFU")]
        public bool DisableTOFU { get; set; }

        public override bool Equals(object obj) => this.Equals(obj as NotaryContentTrust);

        public bool Equals(NotaryContentTrust other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(this.RootJsonPath, other.RootJsonPath) &&
                   Equals(this.RootID, other.RootID) &&
                   Equals(this.RootCertificatePath, other.RootCertificatePath) &&
                   Equals(this.DisableTOFU, other.DisableTOFU);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.RootJsonPath != null ? this.RootJsonPath.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.RootID?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.RootCertificatePath?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ this.DisableTOFU.GetHashCode();
                return hashCode;
            }
        }
    }
}
