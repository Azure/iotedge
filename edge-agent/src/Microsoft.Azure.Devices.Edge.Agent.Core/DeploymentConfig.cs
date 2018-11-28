// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DeploymentConfig : IEquatable<DeploymentConfig>
    {
        static readonly ReadOnlyDictionaryComparer<string, IModule> ModuleDictionaryComparer = new ReadOnlyDictionaryComparer<string, IModule>();

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

        public static DeploymentConfig Empty = new DeploymentConfig(
            "1.0",
            UnknownRuntimeInfo.Instance,
            new SystemModules(UnknownEdgeAgentModule.Instance, UnknownEdgeHubModule.Instance),
            ImmutableDictionary<string, IModule>.Empty);

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; }

        [JsonProperty("runtime")]
        public IRuntimeInfo Runtime { get; }

        [JsonProperty("systemModules")]
        public SystemModules SystemModules { get; }

        [JsonProperty("modules")]
        public IImmutableDictionary<string, IModule> Modules { get; }

        public ModuleSet GetModuleSet()
        {
            var modules = new Dictionary<string, IModule>();
            foreach (KeyValuePair<string, IModule> module in this.Modules)
            {
                modules.Add(module.Key, module.Value);
            }

            this.SystemModules.EdgeHub.Filter(e => e != UnknownEdgeHubModule.Instance).ForEach(h => modules.Add(h.Name, h));
            this.SystemModules.EdgeAgent.Filter(e => e != UnknownEdgeAgentModule.Instance).ForEach(h => modules.Add(h.Name, h));
            return modules.Count == 0
                ? ModuleSet.Empty
                : new ModuleSet(modules);
        }

        public bool Equals(DeploymentConfig other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.SchemaVersion, other.SchemaVersion)
                && Equals(this.Runtime, other.Runtime)
                && Equals(this.SystemModules, other.SystemModules)
                && ModuleDictionaryComparer.Equals(this.Modules, other.Modules);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((DeploymentConfig)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.SchemaVersion != null ? this.SchemaVersion.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Runtime != null ? this.Runtime.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.SystemModules != null ? this.SystemModules.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Modules != null ? this.Modules.GetHashCode() : 0);
                return hashCode;
            }
        }
    }    
}
