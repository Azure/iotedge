// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class TestConfig : IEquatable<TestConfig>
    {
        public TestConfig(string image)
        {
            this.Image = Preconditions.CheckNotNull(image, nameof(image));
        }

        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        public bool Equals(TestConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            return ReferenceEquals(this, other) || string.Equals(this.Image, other.Image);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == this.GetType() && this.Equals((TestConfig)obj);
        }

        public override int GetHashCode() => this.Image?.GetHashCode() ?? 0;
    }

    public class TestModuleBase<TConfig> : IModule<TConfig>
    {
        [JsonConstructor]
        public TestModuleBase(
            string name,
            string version,
            string type,
            ModuleStatus desiredStatus,
            TConfig config,
            RestartPolicy restartPolicy,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configuration,
            IDictionary<string, EnvVal> env)
        {
            this.Name = name;
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.Type = Preconditions.CheckNotNull(type, nameof(type));
            this.DesiredStatus = Preconditions.CheckNotNull(desiredStatus, nameof(desiredStatus));
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
            this.ImagePullPolicy = Preconditions.CheckIsDefined(imagePullPolicy);
            this.ConfigurationInfo = configuration ?? new ConfigurationInfo();
            this.Env = env?.ToImmutableDictionary() ?? ImmutableDictionary<string, EnvVal>.Empty;
        }

        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty(Required = Required.Always, PropertyName = "version")]
        public virtual string Version { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "type")]
        public string Type { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "settings")]
        public TConfig Config { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "restartPolicy")]
        public virtual RestartPolicy RestartPolicy { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "imagePullPolicy")]
        public virtual ImagePullPolicy ImagePullPolicy { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public virtual ModuleStatus DesiredStatus { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "configuration")]
        public ConfigurationInfo ConfigurationInfo { get; }

        [JsonProperty("env")]
        public IDictionary<string, EnvVal> Env { get; }

        public bool OnlyModuleStatusChanged(IModule other) =>
            other is TestModuleBase<TConfig> testModuleBase &&
            string.Equals(this.Name, other.Name) &&
            string.Equals(this.Version, other.Version) &&
            string.Equals(this.Type, other.Type) &&
            this.DesiredStatus != other.DesiredStatus &&
            this.Config.Equals(testModuleBase.Config) &&
            this.RestartPolicy == other.RestartPolicy &&
            this.ImagePullPolicy == other.ImagePullPolicy;

        public override bool Equals(object obj) => this.Equals(obj as TestModuleBase<TConfig>);

        public bool Equals(IModule other) => this.Equals(other as TestModuleBase<TConfig>);

        public virtual bool Equals(IModule<TConfig> other)
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
                   this.ImagePullPolicy == other.ImagePullPolicy;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // We are ignoring this here because, we only change the name of the module on Creation. This
                // is needed because the name is not part of the body of Json equivalent to IModule, it is on the key of the json.
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hashCode = this.Name != null ? this.Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (this.Version != null ? this.Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)this.DesiredStatus;
                hashCode = (hashCode * 397) ^ (this.Config != null ? this.Config.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.RestartPolicy.GetHashCode();
                hashCode = (hashCode * 397) ^ this.ImagePullPolicy.GetHashCode();
                return hashCode;
            }
        }
    }

    public class TestModule : TestModuleBase<TestConfig>
    {
        public TestModule(
            string name,
            string version,
            string type,
            ModuleStatus desiredStatus,
            TestConfig config,
            RestartPolicy restartPolicy,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configuration,
            IDictionary<string, EnvVal> env)
            : base(name, version, type, desiredStatus, config, restartPolicy, imagePullPolicy, configuration, env)
        {
        }

        public TestModule CloneWithImage(string image)
        {
            return new TestModule(this.Name, this.Version, this.Type, this.DesiredStatus, new TestConfig(image), this.RestartPolicy, this.ImagePullPolicy, this.ConfigurationInfo, this.Env);
        }
    }

    public class TestAgentModule : TestModule, IEdgeAgentModule
    {
        public TestAgentModule(string name, string type, TestConfig config, ImagePullPolicy imagePullPolicy, ConfigurationInfo configuration, IDictionary<string, EnvVal> env)
            : base(name ?? Constants.EdgeAgentModuleName, string.Empty, type, ModuleStatus.Running, config, RestartPolicy.Always, imagePullPolicy, configuration, env)
        {
            this.Version = string.Empty;
            this.RestartPolicy = RestartPolicy.Always;
            this.DesiredStatus = ModuleStatus.Running;
        }

        [JsonIgnore]
        public override string Version { get; }

        [JsonIgnore]
        public override RestartPolicy RestartPolicy { get; }

        [JsonIgnore]
        public override ModuleStatus DesiredStatus { get; }

        public virtual IModule WithRuntimeStatus(ModuleStatus newStatus)
        {
            throw new NotImplementedException();
        }
    }

    public class TestHubModule : TestModule, IEdgeHubModule
    {
        public TestHubModule(string name, string type, ModuleStatus desiredStatus, TestConfig config, RestartPolicy restartPolicy, ImagePullPolicy imagePullPolicy, ConfigurationInfo configuration, IDictionary<string, EnvVal> env)
            : base(name ?? Constants.EdgeHubModuleName, string.Empty, type, desiredStatus, config, restartPolicy, imagePullPolicy, configuration, env)
        {
            this.Version = string.Empty;
        }

        [JsonIgnore]
        public override string Version { get; }
    }
}
