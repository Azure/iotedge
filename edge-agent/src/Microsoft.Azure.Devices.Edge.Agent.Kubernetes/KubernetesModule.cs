// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class KubernetesModule : IModule<KubernetesConfig>
    {
        static readonly DictionaryComparer<string, EnvVal> EnvDictionaryComparer = new DictionaryComparer<string, EnvVal>();

        static readonly CombinedKubernetesConfigEqualityComparer ConfigComparer = new CombinedKubernetesConfigEqualityComparer();

        public KubernetesModule(IModule module, KubernetesConfig config)
        {
            this.Name = module.Name;
            this.Version = module.Version;
            this.Type = module.Type;
            this.DesiredStatus = module.DesiredStatus;
            this.RestartPolicy = module.RestartPolicy;
            this.ConfigurationInfo = module.ConfigurationInfo ?? new ConfigurationInfo(string.Empty);
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
            KubernetesConfig settings,
            ImagePullPolicy imagePullPolicy)
        {
            this.Name = name;
            this.Version = version;
            this.Type = type;
            this.DesiredStatus = status;
            this.RestartPolicy = restartPolicy;
            this.ConfigurationInfo = configurationInfo ?? new ConfigurationInfo(string.Empty);
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
        [JsonConverter(typeof(ObjectToStringConverter<KubernetesConfig>))]
        public KubernetesConfig Config { get; }

        public virtual bool Equals(IModule other) => this.Equals(other as KubernetesModule);

        public bool Equals(IModule<KubernetesConfig> other) => this.Equals(other as KubernetesModule);

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
                ConfigComparer.Equals(this.Config, other.Config) &&
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
                hashCode = (hashCode * 397) ^ EnvDictionaryComparer.GetHashCode(this.Env);
                return hashCode;
            }
        }

        public bool IsOnlyModuleStatusChanged(IModule other)
        {
            return other is KubernetesModule &&
                string.Equals(this.Name, other.Name) &&
                string.Equals(this.Version, other.Version) &&
                string.Equals(this.Type, other.Type) &&
                this.DesiredStatus != other.DesiredStatus &&
                ConfigComparer.Equals(this.Config, (other as KubernetesModule).Config) &&
                this.RestartPolicy == other.RestartPolicy &&
                this.ImagePullPolicy == other.ImagePullPolicy &&
                EnvDictionaryComparer.Equals(this.Env, other.Env);
        }

        internal class CombinedKubernetesConfigEqualityComparer : IEqualityComparer<KubernetesConfig>
        {
            static readonly AuthConfigEqualityComparer AuthConfigComparer = new AuthConfigEqualityComparer();

            public bool Equals(KubernetesConfig a, KubernetesConfig b)
            {
                if ((ReferenceEquals(null, a) && !ReferenceEquals(null, b)) ||
                    (!ReferenceEquals(null, a) && ReferenceEquals(null, b)))
                {
                    return false;
                }

                if (ReferenceEquals(a, b))
                {
                    return true;
                }

                string thisOptions = JsonConvert.SerializeObject(a.CreateOptions);
                string otherOptions = JsonConvert.SerializeObject(b.CreateOptions);

                return string.Equals(a.Image, b.Image)
                    && AuthConfigComparer.Equals(a.AuthConfig, b.AuthConfig)
                    && string.Equals(thisOptions, otherOptions);
            }

            public int GetHashCode(KubernetesConfig obj) => obj.GetHashCode();

            internal class AuthConfigEqualityComparer : IEqualityComparer<Option<AuthConfig>>
            {
                public bool Equals(Option<AuthConfig> a, Option<AuthConfig> b)
                {
                    if (!a.HasValue && !b.HasValue)
                    {
                        return true;
                    }

                    if (a.HasValue && b.HasValue)
                    {
                        var authConfig = a.OrDefault();
                        var other = b.OrDefault();

                        return authConfig.Name == other.Name;
                    }

                    return false;
                }

                public int GetHashCode(Option<AuthConfig> obj) => obj.GetHashCode();
            }
        }
    }
}
