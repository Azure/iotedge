// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DeploymentConfig
    {
        [JsonConstructor]
        public DeploymentConfig(
            string schemaVersion,
            IRuntimeInfo runtime,
            SystemModules systemModules,
            IDictionary<string, IModule> modules)
        {
            this.SchemaVersion = Preconditions.CheckNotNull(schemaVersion, nameof(schemaVersion));
            this.SystemModules = Preconditions.CheckNotNull(systemModules, nameof(this.SystemModules));
            this.Runtime = Preconditions.CheckNotNull(runtime, nameof(runtime));
            this.Modules = modules?.ToImmutableDictionary() ?? ImmutableDictionary<string, IModule>.Empty;
            this.UpdateModuleNames();
        }

        void UpdateModuleNames()
        {
            foreach (KeyValuePair<string, IModule> module in this.Modules)
            {
                module.Value.Name = module.Key;
            }
        }

        public static DeploymentConfig Empty = new DeploymentConfig("1.0",
            UnknownRuntimeInfo.Instance,
            new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance),
            ImmutableDictionary<string, IModule>.Empty);

        public string SchemaVersion { get; }

        public IRuntimeInfo Runtime { get; }

        public SystemModules SystemModules { get; }

        public IImmutableDictionary<string, IModule> Modules { get; }

        public ModuleSet GetModuleSet()
        {
            var modules = new Dictionary<string, IModule>();
            foreach (KeyValuePair<string, IModule> module in this.Modules)
            {
                modules.Add(module.Key, module.Value);
            }

            if (this.SystemModules.EdgeHub != null)
            {
                modules.Add(this.SystemModules.EdgeHub.Name, this.SystemModules.EdgeHub);
            }
            return modules.Count == 0 ? ModuleSet.Empty : new ModuleSet(modules);
        }
    }

    public class SystemModules : IEquatable<SystemModules>
    {
        [JsonConstructor]
        public SystemModules(IEdgeAgentModule edgeAgent, IEdgeHubModule edgeHub)
        {
            this.EdgeAgent = edgeAgent;
            this.EdgeHub = edgeHub;
        }

        [JsonProperty(PropertyName = "edgeHub")]
        public IEdgeHubModule EdgeHub { get; }

        [JsonProperty(PropertyName = "edgeAgent")]
        public IEdgeAgentModule EdgeAgent { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as SystemModules);
        }

        public bool Equals(SystemModules other)
        {
            return other != null &&
                EqualityComparer<IEdgeHubModule>.Default.Equals(EdgeHub, other.EdgeHub) &&
                EqualityComparer<IEdgeAgentModule>.Default.Equals(EdgeAgent, other.EdgeAgent);
        }

        public override int GetHashCode()
        {
            var hashCode = -874519432;
            hashCode = hashCode * -1521134295 + EqualityComparer<IEdgeHubModule>.Default.GetHashCode(EdgeHub);
            hashCode = hashCode * -1521134295 + EqualityComparer<IEdgeAgentModule>.Default.GetHashCode(EdgeAgent);
            return hashCode;
        }

        public static bool operator ==(SystemModules modules1, SystemModules modules2)
        {
            return EqualityComparer<SystemModules>.Default.Equals(modules1, modules2);
        }

        public static bool operator !=(SystemModules modules1, SystemModules modules2)
        {
            return !(modules1 == modules2);
        }

        public SystemModules Clone() => new SystemModules(this.EdgeAgent, this.EdgeHub);
    }
}
