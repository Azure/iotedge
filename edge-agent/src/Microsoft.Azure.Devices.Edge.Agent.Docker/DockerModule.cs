// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerModule : IModule<DockerConfig>
    {
        static readonly DictionaryComparer<string, EnvVal> EnvDictionaryComparer = new DictionaryComparer<string, EnvVal>();

        public DockerModule(
            string name,
            string version,
            ModuleStatus desiredStatus,
            RestartPolicy restartPolicy,
            DockerConfig config,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configurationInfo,
            IDictionary<string, EnvVal> env)
        {
            this.Name = name;
            this.Version = version ?? string.Empty;
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
            this.ImagePullPolicy = Preconditions.CheckIsDefined(imagePullPolicy);
            this.ConfigurationInfo = configurationInfo ?? new ConfigurationInfo(string.Empty);
            this.Env = env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
        }

        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        public virtual string Version { get; }

        [JsonProperty(PropertyName = "status")]
        public virtual ModuleStatus DesiredStatus { get; }

        [JsonProperty(PropertyName = "restartPolicy")]
        public virtual RestartPolicy RestartPolicy { get; }

        [JsonProperty(PropertyName = "imagePullPolicy")]
        public virtual ImagePullPolicy ImagePullPolicy { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "type")]
        public virtual string Type => "docker";

        [JsonProperty(Required = Required.Always, PropertyName = "settings")]
        public DockerConfig Config { get; }

        [JsonIgnore]
        public virtual ConfigurationInfo ConfigurationInfo { get; }

        [JsonProperty(PropertyName = "env")]
        public IDictionary<string, EnvVal> Env { get; }

        public override bool Equals(object obj) => this.Equals(obj as DockerModule);

        public virtual bool Equals(IModule other) => this.Equals(other as DockerModule);

        public virtual bool Equals(IModule<DockerConfig> other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Name, other.Name) &&
                   string.Equals(this.Version, other.Version) &&
                   string.Equals(this.Type, other.Type) &&
                   this.DesiredStatus == other.DesiredStatus &&
                   this.Config.Equals(other.Config) &&
                   this.RestartPolicy == other.RestartPolicy &&
                   this.ImagePullPolicy == other.ImagePullPolicy &&
                   EnvDictionaryComparer.Equals(this.Env, other.Env);
        }

        public virtual bool OnlyModuleStatusChanged(IModule other)
        {
            return other is DockerModule dockerModule &&
                string.Equals(this.Name, other.Name) &&
                string.Equals(this.Version, other.Version) &&
                string.Equals(this.Type, other.Type) &&
                this.DesiredStatus != other.DesiredStatus &&
                this.Config.Equals(dockerModule.Config) &&
                this.RestartPolicy == other.RestartPolicy &&
                this.ImagePullPolicy == other.ImagePullPolicy &&
                EnvDictionaryComparer.Equals(this.Env, other.Env);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // We are ignoring this here because, we only change the name of the module on Creation. This
                // is needed because the name is not part of the body of Json equivalent to IModule, it is on the key of the json.
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hashCode = this.Name != null ? this.Name.GetHashCode() : 0;
                // ReSharper restore NonReadonlyMemberInGetHashCode
                hashCode = (hashCode * 397) ^ (this.Version != null ? this.Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.DesiredStatus;
                hashCode = (hashCode * 397) ^ (this.Config != null ? this.Config.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.RestartPolicy.GetHashCode();
                hashCode = (hashCode * 397) ^ this.ImagePullPolicy.GetHashCode();
                hashCode = (hashCode * 397) ^ EnvDictionaryComparer.GetHashCode(this.Env);
                return hashCode;
            }
        }
    }
}
