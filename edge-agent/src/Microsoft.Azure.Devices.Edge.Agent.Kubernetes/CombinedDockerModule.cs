// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class CombinedDockerModule : IModule<CombinedDockerConfig>
    {
        static readonly DictionaryComparer<string, EnvVal> EnvDictionaryComparer = new DictionaryComparer<string, EnvVal>();

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
        public CombinedDockerConfig Config { get; }

        [JsonIgnore]
        public virtual ConfigurationInfo ConfigurationInfo { get; }

        [JsonProperty(PropertyName = "env")]
        public IDictionary<string, EnvVal> Env { get; }

        public CombinedDockerModule(string name, string version, ModuleStatus desiredStatus, RestartPolicy restartPolicy,
            CombinedDockerConfig settings, ConfigurationInfo configurationInfo, IDictionary<string, EnvVal> env)
        {
            this.Name = name;
            this.Version = version ?? string.Empty;
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.Config = Preconditions.CheckNotNull(settings, nameof(settings));
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
            this.ConfigurationInfo = configurationInfo ?? new ConfigurationInfo(string.Empty);
            this.Env = env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
        }

        [JsonConstructor]
        public CombinedDockerModule(string version, ModuleStatus status, RestartPolicy restartPolicy, string type,
            CombinedDockerConfig settings, IDictionary<string, EnvVal> env)
        {
            this.Name = null;
            this.Version = version ?? string.Empty;
            this.DesiredStatus = Preconditions.CheckIsDefined(status);
            this.Config = Preconditions.CheckNotNull(settings, nameof(settings));
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
            this.ConfigurationInfo = new ConfigurationInfo(string.Empty);
            this.Env = env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
        }

        public override bool Equals(object obj) => this.Equals(obj as CombinedDockerModule);

        public virtual bool Equals(IModule other) => this.Equals(other as CombinedDockerModule);

        public virtual bool Equals(IModule<CombinedDockerConfig> other)
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
                EnvDictionaryComparer.Equals(this.Env, other.Env);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                //We are ignoring this here because, we only change the name of the module on Creation. This
                //is needed because the name is not part of the body of Json equivalent to IModule, it is on the key of the json.
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hashCode = (this.Name != null ? this.Name.GetHashCode() : 0);
                // ReSharper restore NonReadonlyMemberInGetHashCode
                hashCode = (hashCode * 397) ^ (this.Version != null ? this.Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.DesiredStatus;
                hashCode = (hashCode * 397) ^ (this.Config != null ? this.Config.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.RestartPolicy.GetHashCode();
                hashCode = (hashCode * 397) ^ EnvDictionaryComparer.GetHashCode(this.Env);
                return hashCode;
            }
        }
    }
}
