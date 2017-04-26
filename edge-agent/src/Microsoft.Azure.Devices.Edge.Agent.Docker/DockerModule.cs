// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Runtime.Serialization;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerModule : IModule<DockerConfig>
    {
        [JsonProperty(Required = Required.Always)]
        public string Name { get; }

        [JsonProperty(Required = Required.Always)]
        public string Version { get; }

        [JsonProperty(Required = Required.Always)]
        public string Type { get; }

        [JsonProperty(Required = Required.Always)]
        public ModuleStatus Status { get; }

        [JsonProperty(Required = Required.Always)]
        public DockerConfig Config { get; }

        [JsonConstructor]
        public DockerModule(string name, string version, string type, ModuleStatus status, DockerConfig config)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.Type = Preconditions.CheckNotNull(type, nameof(type));
            this.Status = Preconditions.CheckIsDefined(status);
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerModule);

        public bool Equals(IModule other) => this.Equals(other as DockerModule);

        public bool Equals(IModule<DockerConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.Name, other.Name) &&
                string.Equals(this.Version, other.Version) &&
                string.Equals(this.Type, other.Type) &&
                this.Status == other.Status &&
                this.Config.Equals(other.Config);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.Name != null ? this.Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Version != null ? this.Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.Status;
                hashCode = (hashCode * 397) ^ (this.Config != null ? this.Config.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}