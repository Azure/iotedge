// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    public class DockerDesiredModule : DockerModule
    {
        [JsonProperty(Required = Required.Always, PropertyName = "version")]
        public override string Version { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate
        )]
        [DefaultValue(Core.Constants.DefaultRestartPolicy)]
        public override RestartPolicy RestartPolicy { get; }

        [JsonConstructor]
        DockerDesiredModule(string version, ModuleStatus desiredStatus, RestartPolicy restartPolicy, string type, DockerConfig settings, ConfigurationInfo configuration)
            : base(string.Empty, version, desiredStatus, restartPolicy, settings, configuration)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }
    }
}
