// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerRuntimeInfo : IRuntimeInfo<DockerRuntimeConfig>, IEquatable<DockerRuntimeInfo>
    {
        [JsonConstructor]
        public DockerRuntimeInfo(string type, DockerRuntimeConfig config)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.Config = config ?? new DockerRuntimeConfig(string.Empty, string.Empty);
        }

        [JsonProperty("type")]
        public string Type => "docker";

        [JsonProperty("settings")]
        public DockerRuntimeConfig Config { get; }

        public override bool Equals(object obj) => Equals(obj as DockerRuntimeInfo);

        public bool Equals(IRuntimeInfo other) => this.Equals(other as DockerRuntimeInfo);

        public bool Equals(DockerRuntimeInfo other) =>
            other != null &&
                   Type == other.Type &&
                   EqualityComparer<DockerRuntimeConfig>.Default.Equals(this.Config, other.Config);

        public override int GetHashCode()
        {
            var hashCode = -466193572;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Type);
            hashCode = hashCode * -1521134295 + EqualityComparer<DockerRuntimeConfig>.Default.GetHashCode(this.Config);
            return hashCode;
        }

        public static bool operator ==(DockerRuntimeInfo info1, DockerRuntimeInfo info2)
        {
            return EqualityComparer<DockerRuntimeInfo>.Default.Equals(info1, info2);
        }

        public static bool operator !=(DockerRuntimeInfo info1, DockerRuntimeInfo info2)
        {
            return !(info1 == info2);
        }
    }

    public class DockerReportedRuntimeInfo : DockerRuntimeInfo, IEquatable<DockerReportedRuntimeInfo>
    {
        public DockerReportedRuntimeInfo(string type, DockerRuntimeConfig config, DockerPlatformInfo platform)
            : base(type, config)
        {
            this.Platform = Preconditions.CheckNotNull(platform);
        }

        [JsonProperty("platform")]
        public DockerPlatformInfo Platform { get; }

        public override bool Equals(object obj) => Equals(obj as DockerReportedRuntimeInfo);

        public bool Equals(DockerReportedRuntimeInfo other) =>
                   other != null &&
                   base.Equals(other) &&
                   EqualityComparer<DockerPlatformInfo>.Default.Equals(Platform, other.Platform);

        public override int GetHashCode()
        {
            var hashCode = 2079518418;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<DockerPlatformInfo>.Default.GetHashCode(Platform);
            return hashCode;
        }

        public static bool operator ==(DockerReportedRuntimeInfo info1, DockerReportedRuntimeInfo info2) =>
            EqualityComparer<DockerReportedRuntimeInfo>.Default.Equals(info1, info2);

        public static bool operator !=(DockerReportedRuntimeInfo info1, DockerReportedRuntimeInfo info2) =>
            !(info1 == info2);
    }

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

    public class DockerRuntimeConfig : IEquatable<DockerRuntimeConfig>
    {
        [JsonConstructor]
        public DockerRuntimeConfig(string minDockerVersion, string loggingOptions)
        {
            this.MinDockerVersion = Preconditions.CheckNotNull(minDockerVersion, nameof(minDockerVersion));
            this.LoggingOptions = Preconditions.CheckNotNull(loggingOptions, nameof(loggingOptions));
        }

        [JsonProperty("minDockerVersion")]
        public string MinDockerVersion { get; }

        [JsonProperty("loggingOptions")]
        public string LoggingOptions { get; }

        public override bool Equals(object obj) => Equals(obj as DockerRuntimeConfig);

        public bool Equals(DockerRuntimeConfig other) =>
            other != null &&
                   MinDockerVersion == other.MinDockerVersion &&
                   LoggingOptions == other.LoggingOptions;

        public override int GetHashCode()
        {
            var hashCode = 1638046857;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(MinDockerVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(LoggingOptions);
            return hashCode;
        }

        public static bool operator ==(DockerRuntimeConfig config1, DockerRuntimeConfig config2) => EqualityComparer<DockerRuntimeConfig>.Default.Equals(config1, config2);

        public static bool operator !=(DockerRuntimeConfig config1, DockerRuntimeConfig config2) => !(config1 == config2);
    }
}
