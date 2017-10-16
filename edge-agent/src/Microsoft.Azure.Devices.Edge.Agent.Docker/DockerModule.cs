// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerModule : IModule<DockerConfig>
    {
        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        public virtual string Version { get; }

        [JsonProperty(PropertyName = "status")]
        public virtual ModuleStatus DesiredStatus { get; }

        [JsonProperty(PropertyName = "restartPolicy")]
        public virtual RestartPolicy RestartPolicy { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "type")]
        public virtual string Type => "docker";

        [JsonProperty(Required = Required.Always, PropertyName = "settings")]
        public DockerConfig Config { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "configuration")]
        public virtual ConfigurationInfo ConfigurationInfo { get; }

        public DockerModule(string name, string version, ModuleStatus desiredStatus, RestartPolicy restartPolicy, DockerConfig config, ConfigurationInfo configurationInfo)
        {
            this.Name = name;
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
            this.ConfigurationInfo = configurationInfo ?? new ConfigurationInfo(string.Empty);
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
                this.DesiredStatus == other.DesiredStatus &&
                this.Config.Equals(other.Config) &&
                this.RestartPolicy == other.RestartPolicy &&
                this.ConfigurationInfo.Equals(other.ConfigurationInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.Name != null ? this.Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Version != null ? this.Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.DesiredStatus;
                hashCode = (hashCode * 397) ^ (this.Config != null ? this.Config.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.RestartPolicy.GetHashCode();
                return hashCode;
            }
        }
    }

    public class DockerDesiredModule : DockerModule
    {
        [JsonProperty(Required = Required.Always, PropertyName = "version")]
        public override string Version { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate
        )]
        [DefaultValue(Core.Constants.DefaultRestartPolicy)]
        public override RestartPolicy RestartPolicy { get; }

        [JsonConstructor]
        DockerDesiredModule(string version, ModuleStatus desiredStatus, RestartPolicy restartPolicy, string type, DockerConfig settings, ConfigurationInfo configuration)
            : base(string.Empty, version, desiredStatus, restartPolicy, settings, configuration)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }
    }

    public class EdgeHubDockerModule : DockerModule, IEdgeHubModule
    {
        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate
        )]
        [DefaultValue(RestartPolicy.Always)]
        public override RestartPolicy RestartPolicy { get; }

        [JsonConstructor]
        public EdgeHubDockerModule(string type, ModuleStatus status, RestartPolicy restartPolicy, DockerConfig settings, ConfigurationInfo configuration)
            : base(Core.Constants.EdgeHubModuleName, string.Empty, status, restartPolicy, settings, configuration)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.DesiredStatus = Preconditions.CheckIsDefined(status);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }
    }

    public class EdgeAgentDockerModule : DockerModule, IEdgeAgentModule
    {
        [JsonConstructor]
        public EdgeAgentDockerModule(string type, DockerConfig settings, ConfigurationInfo configuration)
            : base(Core.Constants.EdgeAgentModuleName, string.Empty, ModuleStatus.Running, RestartPolicy.Always, settings, configuration)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }
    }

    public class EdgeAgentDockerRuntimeModule : EdgeAgentDockerModule
    {
        public EdgeAgentDockerRuntimeModule(DockerConfig settings, ModuleStatus runtimeStatus, ConfigurationInfo configuration)
            : base("docker", settings, configuration)
        {
            this.RuntimeStatus = runtimeStatus;
        }

        [JsonProperty(PropertyName = "runtimeStatus")]
        public ModuleStatus RuntimeStatus { get; }

        [JsonIgnore]
        public override string Version { get; }

        [JsonIgnore]
        public override ModuleStatus DesiredStatus { get; }

        [JsonIgnore]
        public override RestartPolicy RestartPolicy { get; }

        [JsonIgnore]
        public override string Type => "docker";
    }
}
