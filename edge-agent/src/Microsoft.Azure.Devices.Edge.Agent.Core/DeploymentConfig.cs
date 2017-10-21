// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DeploymentConfig
    {
        [JsonConstructor]
        public DeploymentConfig(string schemaVersion,
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
            foreach(KeyValuePair<string, IModule> module in this.Modules)
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
            foreach(KeyValuePair<string, IModule> module in this.Modules)
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

    public class SystemModules
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
    }    
}
