// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Newtonsoft.Json;

    public class EdgeAgentDockerRuntimeModule : EdgeAgentDockerModule
    {
        [JsonConstructor]
        public EdgeAgentDockerRuntimeModule(
            DockerReportedConfig settings,
            ModuleStatus runtimeStatus,
            DateTime lastStartTimeUtc,
            ConfigurationInfo configuration
        )
            : base("docker", settings, configuration)
        {
            this.RuntimeStatus = runtimeStatus;
            this.LastStartTimeUtc = lastStartTimeUtc;

            // You maybe wondering why we are setting this here again even though
            // the base class does this assignment. This is due to a behavior
            // in C# where if you have an assignment to a read-only virtual property
            // from a base constructor when it has been overriden in a sub-class, the
            // assignment becomes a no-op.  In order to fix this we need to assign
            // this here again so that the correct property assignment happens for real!
            this.ConfigurationInfo = configuration ?? new ConfigurationInfo(string.Empty);
        }

        [JsonProperty(PropertyName = "runtimeStatus")]
        public ModuleStatus RuntimeStatus { get; }

        [JsonProperty(PropertyName = "lastStartTimeUtc")]
        public DateTime LastStartTimeUtc { get; }

        [JsonIgnore]
        public override string Version { get; }

        [JsonIgnore]
        public override ModuleStatus DesiredStatus { get; }

        [JsonIgnore]
        public override RestartPolicy RestartPolicy { get; }

        [JsonProperty(Required = Required.Default, PropertyName = "configuration")]
        public override ConfigurationInfo ConfigurationInfo { get; }
    }
}
