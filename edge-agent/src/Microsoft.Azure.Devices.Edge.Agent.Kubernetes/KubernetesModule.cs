// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class KubernetesModule<TConfig> : IModule<TConfig>
    {
        static readonly DictionaryComparer<string, EnvVal> EnvDictionaryComparer = new DictionaryComparer<string, EnvVal>();

        public KubernetesModule(IModule module)
        {
            this.Name = module.Name;
            this.Version = module.Version;
            this.Type = module.Type;
            this.DesiredStatus = module.DesiredStatus;
            this.RestartPolicy = module.RestartPolicy;
            this.ConfigurationInfo = module.ConfigurationInfo;
            this.Env = module.Env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
            this.ImagePullPolicy = module.ImagePullPolicy;
        }

        public KubernetesModule(IModule<TConfig> module)
        {
            this.Name = module.Name;
            this.Version = module.Version;
            this.Type = module.Type;
            this.DesiredStatus = module.DesiredStatus;
            this.RestartPolicy = module.RestartPolicy;
            this.ConfigurationInfo = module.ConfigurationInfo;
            this.Env = module.Env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
            this.Config = module.Config;
            this.ImagePullPolicy = module.ImagePullPolicy;
        }

        [JsonConstructor]
        public KubernetesModule(string moduleName, string version, string type, ModuleStatus status, RestartPolicy restartPolicy, ConfigurationInfo configurationInfo, IDictionary<string, EnvVal> env, TConfig settings, ImagePullPolicy imagePullPolicy)
        {
            this.Name = moduleName;
            this.Version = version;
            this.Type = type;
            this.DesiredStatus = status;
            this.RestartPolicy = restartPolicy;
            this.ConfigurationInfo = configurationInfo;
            this.Env = env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
            this.Config = settings;
            this.ImagePullPolicy = imagePullPolicy;
        }

        [JsonProperty(PropertyName = "moduleName")]
        public string ModuleName {get{return this.Name;}set{this.Name=value;}}

        public string Name {get;set;}

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
        public TConfig Config { get; set; }

        public virtual bool Equals(IModule other) => this.Equals(other as KubernetesModule<TConfig>);

        public bool Equals(IModule<TConfig> other) => this.Equals(other as KubernetesModule<TConfig>);

        public bool Equals(KubernetesModule<TConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.Name, other.Name) &&
                string.Equals(this.Version, other.Version) &&
                string.Equals(this.Type, other.Type) &&
                this.DesiredStatus != other.DesiredStatus &&
                this.RestartPolicy == other.RestartPolicy &&
                this.ConfigurationInfo == other.ConfigurationInfo &&
                this.ImagePullPolicy == other.ImagePullPolicy &&
                this.Config is CombinedDockerConfig &&
                this.Config as CombinedDockerConfig == other.Config as CombinedDockerConfig &&
                this.Config is string &&
                this.Config as string == other.Config as string &&
                EnvDictionaryComparer.Equals(this.Env, other.Env);
        }

        public bool IsOnlyModuleStatusChanged(IModule other)
        {
            return other is KubernetesModule<TConfig> &&
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
