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
        Paused
    }

    public interface IModule : IEquatable<IModule>
    {
        string Name { get; }

        string Version { get; }

        string Type { get; }

        ModuleStatus Status { get; }
    }

    public interface IModule<TConfig> : IModule, IEquatable<IModule<TConfig>>
    {
        TConfig Config { get; }
    }
}
