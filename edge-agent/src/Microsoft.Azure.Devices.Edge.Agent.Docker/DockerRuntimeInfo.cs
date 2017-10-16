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
                   EqualityComparer<DockerRuntimeConfig>.Default.Equals(Config, other.Config);

        public override int GetHashCode()
        {
            var hashCode = -466193572;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);
            hashCode = hashCode * -1521134295 + EqualityComparer<DockerRuntimeConfig>.Default.GetHashCode(Config);
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

        public static bool operator ==(DockerRuntimeConfig config1, DockerRuntimeConfig config2)
        {
            return EqualityComparer<DockerRuntimeConfig>.Default.Equals(config1, config2);
        }

        public static bool operator !=(DockerRuntimeConfig config1, DockerRuntimeConfig config2)
        {
            return !(config1 == config2);
        }
    }
}
