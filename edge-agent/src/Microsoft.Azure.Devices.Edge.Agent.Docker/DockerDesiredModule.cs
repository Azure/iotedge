// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerDesiredModule : DockerModule
    {
        [JsonConstructor]
        DockerDesiredModule(
            string version,
            ModuleStatus desiredStatus,
            RestartPolicy restartPolicy,
            string type,
            DockerConfig settings,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configuration,
            IDictionary<string, EnvVal> env)
            : base(string.Empty, version, desiredStatus, restartPolicy, settings, imagePullPolicy, configuration, env)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(Core.Constants.DefaultRestartPolicy)]
        public override RestartPolicy RestartPolicy { get; }
    }
}
