// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class TestConfig : IEquatable<TestConfig>
    {
        [JsonProperty(Required = Required.Always)]
        public string Image { get; }

        public TestConfig(string image)
        {
            this.Image = Preconditions.CheckNotNull(image, nameof(image));
        }

        public bool Equals(TestConfig other)
        {
            if (ReferenceEquals(null, other))
                return false;
            return ReferenceEquals(this, other) || string.Equals(this.Image, other.Image);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj.GetType() == this.GetType() && this.Equals((TestConfig)obj);
        }

        public override int GetHashCode() => this.Image?.GetHashCode() ?? 0;
    }

    public class TestModule : IModule<TestConfig>
    {
        [JsonProperty(Required = Required.Always)]
        public string Name { get; }

        [JsonProperty(Required = Required.Always)]
        public string Version { get; }

        [JsonProperty(Required = Required.Always)]
        public string Type { get; }

        [JsonProperty(Required = Required.Always)]
        public ModuleStatus Status { get; }

        [JsonProperty(Required = Required.Always)]
        public TestConfig Config { get; }

        [JsonConstructor]
        public TestModule(string name, string version, string type, ModuleStatus status, TestConfig config)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.Type = Preconditions.CheckNotNull(type, nameof(type));
            this.Status = Preconditions.CheckNotNull(status, nameof(status));
            this.Config = Preconditions.CheckNotNull(config, nameof(config));
        }

        public override bool Equals(object obj) => this.Equals(obj as TestModule);

        public bool Equals(IModule other) => this.Equals(other as TestModule);

        public bool Equals(IModule<TestConfig> other)
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