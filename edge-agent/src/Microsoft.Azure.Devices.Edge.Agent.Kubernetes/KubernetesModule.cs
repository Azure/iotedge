// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using System.Collections.Immutable;

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

        public TConfig Config { get; }

        public virtual bool Equals(IModule other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return this.Equals(other);
        }

        public bool Equals(IModule<TConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return this.Equals(other);
        }

        public bool OnlyModuleStatusChanged(IModule other)
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
