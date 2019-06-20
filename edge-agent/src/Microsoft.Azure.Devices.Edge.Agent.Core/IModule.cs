// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModuleStatus
    {
        /// <summary>
        /// This is the state that all modules start out in. As soon as a deployment
        /// is created, it is assumed that all modules in the deployment begin life
        /// in the "Unknown" state.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown, // TODO: Consider removing this status entirely since it doesn't seem to be used.

        /// <summary>
        /// Modules transition to the "Backoff" state when the MMA has scheduled
        /// the module to be started but hasn't actually started running yet. This is
        /// useful when we have a failing module that is undergoing state changes as
        /// part of the implementation of its restart policy. For example when a failing
        /// module is awaiting restart during the cool-off period as dictated by the
        /// exponential back-off restart strategy, the module will be in this
        /// "Backoff" state.
        /// </summary>
        [EnumMember(Value = "backoff")]
        Backoff,

        /// <summary>
        /// This state indicates that module is currently running.
        /// </summary>
        [EnumMember(Value = "running")]
        Running,

        /// <summary>
        /// The state transitions to "unhealthy" when a health-probe check fails/times out.
        /// </summary>
        [EnumMember(Value = "unhealthy")]
        Unhealthy,

        /// <summary>
        /// The "Stopped" state indicates that the module exited successfully (with a zero
        /// exit code).
        /// </summary>
        [EnumMember(Value = "stopped")]
        Stopped,

        /// <summary>
        /// The "Failed" state indicates that the module exited with a failure exit code
        /// (non-zer0). The module can transition back to "Backoff" from this state
        /// depending on the restart policy in effect.
        /// This state can indicate that the module has experienced an unrecoverable error.
        /// This happens when the MMA has given up on trying to resuscitate the module and user
        /// action is required to update its code/configuration in order for it to work again
        /// which would mean that a new deployment is required.
        /// </summary>
        [EnumMember(Value = "failed")]
        Failed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RestartPolicy
    {
        [EnumMember(Value = "never")]
        Never = 0,

        [EnumMember(Value = "on-failure")]
        OnFailure = 1,

        [EnumMember(Value = "on-unhealthy")]
        OnUnhealthy = 2,

        [EnumMember(Value = "always")]
        Always = 3,

        [EnumMember(Value = "unknown")]
        Unknown
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImagePullPolicy
    {
        [EnumMember(Value = "on-create")]
        OnCreate = 0,

        [EnumMember(Value = "never")]
        Never = 1,
    }

    public interface IModule : IEquatable<IModule>
    {
        [JsonIgnore]
        string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        string Version { get; }

        [JsonProperty(PropertyName = "type")]
        string Type { get; }

        [JsonProperty(PropertyName = "status")]
        ModuleStatus DesiredStatus { get; }

        [JsonProperty(PropertyName = "restartPolicy")]
        RestartPolicy RestartPolicy { get; }

        [JsonProperty(PropertyName = "imagePullPolicy")]
        ImagePullPolicy ImagePullPolicy { get; }

        [JsonIgnore]
        ConfigurationInfo ConfigurationInfo { get; }

        [JsonProperty(PropertyName = "env")]
        IDictionary<string, EnvVal> Env { get; }

        bool OnlyModuleStatusChanged(IModule other);
    }

    public interface IModule<TConfig> : IModule, IEquatable<IModule<TConfig>>
    {
        [JsonProperty(PropertyName = "settings")]
        TConfig Config { get; }
    }

    public interface IEdgeHubModule : IModule
    {
    }

    public interface IEdgeAgentModule : IModule
    {
    }
}
