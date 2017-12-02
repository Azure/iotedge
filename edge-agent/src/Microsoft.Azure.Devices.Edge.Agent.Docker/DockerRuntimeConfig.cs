// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

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

        public override bool Equals(object obj) => this.Equals(obj as DockerRuntimeConfig);

        public bool Equals(DockerRuntimeConfig other) =>
            other != null && this.MinDockerVersion == other.MinDockerVersion && this.LoggingOptions == other.LoggingOptions;

        public override int GetHashCode()
        {
            int hashCode = 1638046857;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.MinDockerVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.LoggingOptions);
            return hashCode;
        }

        public static bool operator ==(DockerRuntimeConfig config1, DockerRuntimeConfig config2) => EqualityComparer<DockerRuntimeConfig>.Default.Equals(config1, config2);

        public static bool operator !=(DockerRuntimeConfig config1, DockerRuntimeConfig config2) => !(config1 == config2);
    }
}
