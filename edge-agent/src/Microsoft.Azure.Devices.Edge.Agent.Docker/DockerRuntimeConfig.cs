// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Newtonsoft.Json;

    public class DockerRuntimeConfig : IEquatable<DockerRuntimeConfig>
    {
        public DockerRuntimeConfig(string minDockerVersion, string loggingOptions)
            : this(minDockerVersion, null, loggingOptions)
        {
        }

        [JsonConstructor]
        public DockerRuntimeConfig(string minDockerVersion, IDictionary<string, RegistryCredentials> registryCredentials, string loggingOptions = "")
        {
            this.MinDockerVersion = minDockerVersion ?? string.Empty;
            this.LoggingOptions = loggingOptions;
            this.RegistryCredentials = registryCredentials?.ToImmutableDictionary() ?? ImmutableDictionary<string, RegistryCredentials>.Empty;
        }

        [JsonProperty("minDockerVersion")]
        public string MinDockerVersion { get; }

        [JsonProperty("loggingOptions")]
        public string LoggingOptions { get; }

        [JsonProperty("registryCredentials")]
        public IDictionary<string, RegistryCredentials> RegistryCredentials { get; }

        public static bool operator ==(DockerRuntimeConfig config1, DockerRuntimeConfig config2) => EqualityComparer<DockerRuntimeConfig>.Default.Equals(config1, config2);

        public static bool operator !=(DockerRuntimeConfig config1, DockerRuntimeConfig config2) => !(config1 == config2);

        public override bool Equals(object obj) => this.Equals(obj as DockerRuntimeConfig);

        public bool Equals(DockerRuntimeConfig other) =>
            other != null && this.MinDockerVersion == other.MinDockerVersion && this.LoggingOptions == other.LoggingOptions &&
            EqualityComparer<IDictionary<string, RegistryCredentials>>.Default.Equals(this.RegistryCredentials, other.RegistryCredentials);

        public override int GetHashCode()
        {
            int hashCode = 1638046857;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.MinDockerVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.LoggingOptions);
            hashCode = hashCode * -1521134295 + EqualityComparer<IDictionary<string, RegistryCredentials>>.Default.GetHashCode(this.RegistryCredentials);
            return hashCode;
        }
    }
}
