// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class AgentConfig
    {
        public AgentConfig(long version, IRuntimeInfo runtimeInfo, ModuleSet moduleSet, Option<IEdgeAgentModule> edgeAgent)
        {
            this.Version = version;
            this.Runtime = runtimeInfo;
            this.ModuleSet = moduleSet ?? ModuleSet.Empty;
            this.EdgeAgent = edgeAgent;
        }

        public static AgentConfig Empty { get; } = new AgentConfig(0, UnknownRuntimeInfo.Instance, ModuleSet.Empty, Option.None<IEdgeAgentModule>());

        public long Version { get; }

        public IRuntimeInfo Runtime { get; }

        public ModuleSet ModuleSet { get; }

        public Option<IEdgeAgentModule> EdgeAgent { get; }
    }
}
