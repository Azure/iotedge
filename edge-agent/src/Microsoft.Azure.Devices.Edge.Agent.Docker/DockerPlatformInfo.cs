// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class DockerPlatformInfo : IEquatable<DockerPlatformInfo>
    {
        [JsonConstructor]
        public DockerPlatformInfo(string operatingSystemType, string architecture, string version)
        {
            this.OperatingSystemType = operatingSystemType ?? string.Empty;
            this.Architecture = architecture ?? string.Empty;
            this.Version = version ?? string.Empty;
        }

        [JsonProperty("os")]
        public string OperatingSystemType { get; }

        [JsonProperty("architecture")]
        public string Architecture { get; }

        [JsonProperty("version")]
        public string Version { get; }

        public override bool Equals(object obj) => this.Equals(obj as DockerPlatformInfo);

        public bool Equals(DockerPlatformInfo other) =>
                   other != null &&
                   this.OperatingSystemType == other.OperatingSystemType &&
                   this.Architecture == other.Architecture &&
                   this.Version == other.Version;

        public override int GetHashCode()
        {
            int hashCode = 577840947;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.OperatingSystemType);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Architecture);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Version);
            return hashCode;
        }

        public static bool operator ==(DockerPlatformInfo info1, DockerPlatformInfo info2) => EqualityComparer<DockerPlatformInfo>.Default.Equals(info1, info2);

        public static bool operator !=(DockerPlatformInfo info1, DockerPlatformInfo info2) => !(info1 == info2);
    }
}
