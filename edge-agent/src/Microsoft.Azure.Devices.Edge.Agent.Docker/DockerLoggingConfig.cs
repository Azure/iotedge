// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerLoggingConfig
    {
        public DockerLoggingConfig(string type)
            : this(type, ImmutableDictionary<string, string>.Empty)
        {
        }

        [JsonConstructor]
        public DockerLoggingConfig(string type, IDictionary<string, string> config)
        {
            this.Type = Preconditions.CheckNonWhiteSpace(type, nameof(type));
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
        }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "config")]
        public IDictionary<string, string> Config { get; }

        public override bool Equals(object obj) => this.Equals(obj as DockerLoggingConfig);

        public bool Equals(DockerLoggingConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.ConfigEquals(this.Config, other.Config) &&
                   string.Equals(this.Type, other.Type);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Type.GetHashCode();
                hashCode = (hashCode * 397) ^ string.Join(";", this.Config.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}")).GetHashCode();
                return hashCode;
            }
        }

        bool ConfigEquals(IDictionary<string, string> config1, IDictionary<string, string> config2) =>
            config1.Count == config2.Count && !config1.Except(config2).Any();
    }
}
