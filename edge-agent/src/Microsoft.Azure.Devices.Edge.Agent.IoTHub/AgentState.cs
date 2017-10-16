// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class AgentState : IEquatable<AgentState>
    {
        static readonly DictionaryComparer<string, IModule> StringModuleDictionaryComparer = new DictionaryComparer<string, IModule>();

        [JsonConstructor]
        public AgentState(long lastDesiredVersion = 0, DeploymentStatus lastDesiredStatus = null, IRuntimeInfo runtimeInfo = null, IDictionary<string, IModule> systemModules = null, IDictionary<string, IModule> modules = null)
        {
            this.LastDesiredVersion = lastDesiredVersion;
            this.LastDesiredStatus = lastDesiredStatus ?? DeploymentStatus.Unknown;
            this.RuntimeInfo = runtimeInfo;
            this.SystemModules = systemModules?.ToImmutableDictionary() ?? ImmutableDictionary<string, IModule>.Empty;
            this.Modules = modules?.ToImmutableDictionary() ?? ImmutableDictionary<string, IModule>.Empty;
        }

        [JsonProperty(PropertyName = "lastDesiredVersion")]
        [DefaultValue(0)]
        public long LastDesiredVersion { get; }

        [JsonProperty(PropertyName = "lastDesiredStatus")]
        public DeploymentStatus LastDesiredStatus { get; }

        [JsonProperty(PropertyName = "runtime")]
        public IRuntimeInfo RuntimeInfo { get; }

        [JsonProperty(PropertyName = "systemModules")]
        public IImmutableDictionary<string, IModule> SystemModules { get; }

        [JsonProperty(PropertyName = "modules")]
        public IImmutableDictionary<string, IModule> Modules { get; }

        public AgentState Clone() => new AgentState(
            this.LastDesiredVersion,
            this.LastDesiredStatus.Clone(),
            this.RuntimeInfo,
            this.SystemModules.ToImmutableDictionary(),
            this.Modules.ToImmutableDictionary()
        );

        public override bool Equals(object obj) => this.Equals(obj as AgentState);

        public bool Equals(AgentState other) =>
                   other != null &&
                   this.LastDesiredVersion == other.LastDesiredVersion &&
                   EqualityComparer<DeploymentStatus>.Default.Equals(this.LastDesiredStatus, other.LastDesiredStatus) &&
                   EqualityComparer<IRuntimeInfo>.Default.Equals(this.RuntimeInfo, other.RuntimeInfo) &&
                   StringModuleDictionaryComparer.Equals(this.SystemModules.ToImmutableDictionary(), other.SystemModules.ToImmutableDictionary()) &&
                   StringModuleDictionaryComparer.Equals(this.Modules.ToImmutableDictionary(), other.Modules.ToImmutableDictionary());

        public override int GetHashCode()
        {
            var hashCode = -1995647028;
            hashCode = hashCode * -1521134295 + LastDesiredVersion.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<DeploymentStatus>.Default.GetHashCode(LastDesiredStatus);
            hashCode = hashCode * -1521134295 + EqualityComparer<IRuntimeInfo>.Default.GetHashCode(RuntimeInfo);
            hashCode = hashCode * -1521134295 + StringModuleDictionaryComparer.GetHashCode(SystemModules.ToImmutableDictionary());
            hashCode = hashCode * -1521134295 + StringModuleDictionaryComparer.GetHashCode(Modules.ToImmutableDictionary());
            return hashCode;
        }

        public static bool operator ==(AgentState state1, AgentState state2)
        {
            return EqualityComparer<AgentState>.Default.Equals(state1, state2);
        }

        public static bool operator !=(AgentState state1, AgentState state2)
        {
            return !(state1 == state2);
        }
    }
}
