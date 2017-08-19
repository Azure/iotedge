// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerModule : IModule<DockerConfig>
    {
        [JsonProperty(Required = Required.Always, PropertyName = "name")]
        public string Name { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "version")]
        public string Version { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "type")]
        public string Type => "docker";

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public ModuleStatus Status { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "config")]
        public DockerConfig Config { get; }

        public DockerModule(string name, string version, ModuleStatus status, DockerConfig config)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.Status = Preconditions.CheckIsDefined(status);
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
        }

        [JsonConstructor]
        DockerModule(string name, string version, string type, ModuleStatus status, DockerConfig config)
            : this(name, version, status, config)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerModule);

        public virtual bool Equals(IModule other) => this.Equals(other as DockerModule);

        public virtual bool Equals(IModule<DockerConfig> other)
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
