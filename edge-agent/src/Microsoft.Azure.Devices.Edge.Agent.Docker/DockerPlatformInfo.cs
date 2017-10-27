// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class DockerPlatformInfo : IEquatable<DockerPlatformInfo>
    {
        [JsonConstructor]
        public DockerPlatformInfo(string operatingSystemType, string architecture)
        {
            this.OperatingSystemType = operatingSystemType ?? string.Empty;
            this.Architecture = architecture ?? string.Empty;
        }

        [JsonProperty("os")]
        public string OperatingSystemType { get; }

        [JsonProperty("architecture")]
        public string Architecture { get; }

        public override bool Equals(object obj) => Equals(obj as DockerPlatformInfo);

        public bool Equals(DockerPlatformInfo other) =>
                   other != null &&
                   OperatingSystemType == other.OperatingSystemType &&
                   Architecture == other.Architecture;

        public override int GetHashCode()
        {
            var hashCode = 577840947;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(OperatingSystemType);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Architecture);
            return hashCode;
        }

        public static bool operator ==(DockerPlatformInfo info1, DockerPlatformInfo info2) => EqualityComparer<DockerPlatformInfo>.Default.Equals(info1, info2);

        public static bool operator !=(DockerPlatformInfo info1, DockerPlatformInfo info2) => !(info1 == info2);
    }
}
