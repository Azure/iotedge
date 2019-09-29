// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class KubernetesModule : IModule<CombinedDockerConfig>
    {
        static readonly DictionaryComparer<string, EnvVal> EnvDictionaryComparer = new DictionaryComparer<string, EnvVal>();

        public KubernetesModule(IModule module, CombinedDockerConfig config)
        {
            this.Name = module.Name;
            this.Version = module.Version;
            this.Type = module.Type;
            this.DesiredStatus = module.DesiredStatus;
            this.RestartPolicy = module.RestartPolicy;
            this.ConfigurationInfo = module.ConfigurationInfo;
            this.Env = module.Env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
            this.ImagePullPolicy = module.ImagePullPolicy;
            this.Config = config;
        }

        [JsonConstructor]
        public KubernetesModule(
            string name,
            string version,
            string type,
            ModuleStatus status,
            RestartPolicy restartPolicy,
            ConfigurationInfo configurationInfo,
            IDictionary<string, EnvVal> env,
            CombinedDockerConfig settings,
            ImagePullPolicy imagePullPolicy)
        {
            this.Name = name;
            this.Version = version;
            this.Type = type;
            this.DesiredStatus = status;
            this.RestartPolicy = restartPolicy;
            this.ConfigurationInfo = configurationInfo;
            this.Env = env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
            this.Config = settings;
            this.ImagePullPolicy = imagePullPolicy;
        }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        public string Version { get; }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; }

        [JsonProperty(PropertyName = "status")]
        public ModuleStatus DesiredStatus { get; }

        [JsonProperty(PropertyName = "restartPolicy")]
        public RestartPolicy RestartPolicy { get; }

        [JsonProperty(PropertyName = "imagePullPolicy")]
        public ImagePullPolicy ImagePullPolicy { get; }

        [JsonIgnore]
        public ConfigurationInfo ConfigurationInfo { get; }

        [JsonProperty(PropertyName = "env")]
        public IDictionary<string, EnvVal> Env { get; }

        [JsonProperty(PropertyName = "settings")]
        [JsonConverter(typeof(CombinedDockerConfigToStringConverter))]
        public CombinedDockerConfig Config { get; }

        public virtual bool Equals(IModule other) => this.Equals(other as KubernetesModule);

        public bool Equals(IModule<CombinedDockerConfig> other) => this.Equals(other as KubernetesModule);

        public bool Equals(KubernetesModule other)
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
                this.RestartPolicy == other.RestartPolicy &&
                Equals(this.ConfigurationInfo, other.ConfigurationInfo) &&
                this.ImagePullPolicy == other.ImagePullPolicy &&
                EnvDictionaryComparer.Equals(this.Env, other.Env);
        }

        public bool IsOnlyModuleStatusChanged(IModule other)
        {
            return other is KubernetesModule &&
                string.Equals(this.Name, other.Name) &&
                string.Equals(this.Version, other.Version) &&
                string.Equals(this.Type, other.Type) &&
                this.DesiredStatus != other.DesiredStatus &&
                this.RestartPolicy == other.RestartPolicy &&
                this.ImagePullPolicy == other.ImagePullPolicy &&
                EnvDictionaryComparer.Equals(this.Env, other.Env);
        }
    }
}
