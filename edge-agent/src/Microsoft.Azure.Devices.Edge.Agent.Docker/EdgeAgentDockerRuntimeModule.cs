// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Newtonsoft.Json;

    public class EdgeAgentDockerRuntimeModule : EdgeAgentDockerModule
    {
        public EdgeAgentDockerRuntimeModule(DockerConfig settings, ModuleStatus runtimeStatus, ConfigurationInfo configuration)
            : base("docker", settings, configuration)
        {
            this.RuntimeStatus = runtimeStatus;
        }

        [JsonProperty(PropertyName = "runtimeStatus")]
        public ModuleStatus RuntimeStatus { get; }

        [JsonIgnore]
        public override string Version { get; }

        [JsonIgnore]
        public override ModuleStatus DesiredStatus { get; }

        [JsonIgnore]
        public override RestartPolicy RestartPolicy { get; }
    }
}
