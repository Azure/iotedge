// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModuleStatus
    {
        [EnumMember(Value = "unknown")]
        Unknown,
        [EnumMember(Value = "running")]
        Running,
        [EnumMember(Value = "stopped")]
        Stopped,
        [EnumMember(Value = "paused")]
        Paused,
        [EnumMember(Value = "unhealthy")]
        Unhealthy,
    }

    public interface IModule : IEquatable<IModule>
    {
        [JsonProperty(PropertyName = "name")]
        string Name { get; }

        [JsonProperty(PropertyName = "version")]
        string Version { get; }

        [JsonProperty(PropertyName = "type")]
        string Type { get; }

        [JsonProperty(PropertyName = "status")]
        ModuleStatus Status { get; }

    }

    public interface IModule<TConfig> : IModule, IEquatable<IModule<TConfig>>
    {
        TConfig Config { get; }
    }
}
