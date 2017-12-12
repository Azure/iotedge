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

        public static AgentState Empty = new AgentState();

        [JsonConstructor]
        public AgentState(
            long lastDesiredVersion = 0,
            DeploymentStatus lastDesiredStatus = null,
            IRuntimeInfo runtimeInfo = null,
            SystemModules systemModules = null,
            IDictionary<string, IModule> modules = null,
            string schemaVersion = "",
            VersionInfo version = null
        )
        {
            this.SchemaVersion = schemaVersion ?? string.Empty;
            this.Version = version ?? VersionInfo.Empty;
            this.LastDesiredVersion = lastDesiredVersion;
            this.LastDesiredStatus = lastDesiredStatus ?? DeploymentStatus.Unknown;
            this.RuntimeInfo = runtimeInfo;
            this.SystemModules = systemModules ?? new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance);
            this.Modules = modules?.ToImmutableDictionary() ?? ImmutableDictionary<string, IModule>.Empty;
        }

        [JsonProperty(PropertyName = "schemaVersion")]
        public string SchemaVersion { get; }

        [JsonProperty(PropertyName = "version")]
        public VersionInfo Version { get; }

        [JsonProperty(PropertyName = "lastDesiredVersion")]
        [DefaultValue(0)]
        public long LastDesiredVersion { get; }

        [JsonProperty(PropertyName = "lastDesiredStatus")]
        public DeploymentStatus LastDesiredStatus { get; }

        [JsonProperty(PropertyName = "runtime")]
        public IRuntimeInfo RuntimeInfo { get; }

        [JsonProperty(PropertyName = "systemModules")]
        public SystemModules SystemModules { get; }

        [JsonProperty(PropertyName = "modules")]
        public IImmutableDictionary<string, IModule> Modules { get; }

        public AgentState Clone() => new AgentState(
            this.LastDesiredVersion,
            this.LastDesiredStatus.Clone(),
            this.RuntimeInfo,
            this.SystemModules.Clone(),
            this.Modules.ToImmutableDictionary(),
            this.SchemaVersion,
            this.Version
        );

        public override bool Equals(object obj) => this.Equals(obj as AgentState);

        public bool Equals(AgentState other) =>
                   other != null &&
                   this.LastDesiredVersion == other.LastDesiredVersion &&
                   this.SchemaVersion == other.SchemaVersion &&
                   EqualityComparer<VersionInfo>.Default.Equals(this.Version, other.Version) &&
                   EqualityComparer<DeploymentStatus>.Default.Equals(this.LastDesiredStatus, other.LastDesiredStatus) &&
                   EqualityComparer<IRuntimeInfo>.Default.Equals(this.RuntimeInfo, other.RuntimeInfo) &&
                   this.SystemModules.Equals(other.SystemModules) &&
                   StringModuleDictionaryComparer.Equals(this.Modules.ToImmutableDictionary(), other.Modules.ToImmutableDictionary());

        public override int GetHashCode()
        {
            int hashCode = -1995647028;
            hashCode = hashCode * -1521134295 + this.LastDesiredVersion.GetHashCode();
            hashCode = hashCode * -1521134295 + this.SchemaVersion.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<VersionInfo>.Default.GetHashCode(this.Version);
            hashCode = hashCode * -1521134295 + EqualityComparer<DeploymentStatus>.Default.GetHashCode(this.LastDesiredStatus);
            hashCode = hashCode * -1521134295 + EqualityComparer<IRuntimeInfo>.Default.GetHashCode(this.RuntimeInfo);
            hashCode = hashCode * -1521134295 + this.SystemModules.GetHashCode();
            hashCode = hashCode * -1521134295 + StringModuleDictionaryComparer.GetHashCode(this.Modules.ToImmutableDictionary());
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
