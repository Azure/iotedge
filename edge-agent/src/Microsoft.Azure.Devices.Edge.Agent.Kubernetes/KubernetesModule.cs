// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KubernetesModule<TConfig> : IModule<TConfig>
    {
        static readonly DictionaryComparer<string, EnvVal> EnvDictionaryComparer = new DictionaryComparer<string, EnvVal>();

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
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string Type { get; set; }

        public ModuleStatus DesiredStatus { get; set; }

        public RestartPolicy RestartPolicy { get; set; }

        public ConfigurationInfo ConfigurationInfo { get; set; }

        public IDictionary<string, EnvVal> Env { get; set; }

        public ImagePullPolicy ImagePullPolicy { get; set; }

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
                this.Config is DockerConfig &&
                this.Config as DockerConfig == other.Config as DockerConfig &&
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
