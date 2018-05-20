// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeHubDockerModule : DockerModule, IEdgeHubModule
    {
        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate
        )]
        [DefaultValue(RestartPolicy.Always)]
        public override RestartPolicy RestartPolicy { get; }

        [JsonConstructor]
        public EdgeHubDockerModule(string type, ModuleStatus status, RestartPolicy restartPolicy,
            DockerConfig settings, ConfigurationInfo configuration, IDictionary<string, EnvVal> env, string version = "")
            : base(Core.Constants.EdgeHubModuleName, version, status, restartPolicy, settings, configuration, env)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.DesiredStatus = Preconditions.CheckIsDefined(status);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }
    }
}
