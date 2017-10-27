// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeAgentDockerModule : DockerModule, IEdgeAgentModule
    {
        [JsonConstructor]
        public EdgeAgentDockerModule(string type, DockerConfig settings, ConfigurationInfo configuration)
            : base(Core.Constants.EdgeAgentModuleName, string.Empty, ModuleStatus.Running, RestartPolicy.Always, settings, configuration)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }
    }
}
