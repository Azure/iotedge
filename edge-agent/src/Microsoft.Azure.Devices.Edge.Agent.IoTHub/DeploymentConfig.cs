// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
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
            this.SystemModules = Preconditions.CheckNotNull(systemModules, nameof(SystemModules));
            this.Runtime = Preconditions.CheckNotNull(runtime, nameof(runtime));
            this.Modules = modules?.ToImmutableDictionary() ?? ImmutableDictionary<string, IModule>.Empty;
        }

        public string SchemaVersion { get; }

        public IRuntimeInfo Runtime { get; }

        public SystemModules SystemModules { get; }

        public IImmutableDictionary<string, IModule> Modules { get; }
    }

    public class SystemModules
    {
        [JsonConstructor]
        public SystemModules(IEdgeAgentModule edgeAgent, IEdgeHubModule edgeHub)
        {
            this.EdgeAgent = Preconditions.CheckNotNull(edgeAgent, nameof(edgeAgent));
            this.EdgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
        }

        [JsonProperty(PropertyName = "edgeHub")]
        public IEdgeHubModule EdgeHub { get; }

        [JsonProperty(PropertyName = "edgeAgent")]
        public IEdgeAgentModule EdgeAgent { get; }
    }
}
