// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class UnknownModule : IModule
    {
        public string Type => "Unknown";

        public string Name { get => "Unknown"; set { } }

        public string Version => string.Empty;

        public ModuleStatus DesiredStatus => ModuleStatus.Unknown;

        public RestartPolicy RestartPolicy => RestartPolicy.Never;

        public ConfigurationInfo ConfigurationInfo => new ConfigurationInfo();

        public bool Equals(IModule other) => other != null && object.ReferenceEquals(this, other);
    }

    public class UnknownEdgeHubModule : UnknownModule, IEdgeHubModule
    {
        UnknownEdgeHubModule() { }

        public static UnknownEdgeHubModule Instance { get; } = new UnknownEdgeHubModule();
    }

    public class UnknownEdgeAgentModule : UnknownModule, IEdgeAgentModule
    {
        UnknownEdgeAgentModule() { }

        public static UnknownEdgeAgentModule Instance { get; } = new UnknownEdgeAgentModule();
    }
}
