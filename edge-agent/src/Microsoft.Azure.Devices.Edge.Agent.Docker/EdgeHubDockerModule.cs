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
        [JsonConstructor]
        public EdgeHubDockerModule(
            string type,
            ModuleStatus status,
            RestartPolicy restartPolicy,
            DockerConfig settings,
            ImagePullPolicy imagePullPolicy,
            uint startupOrder,
            ConfigurationInfo configuration,
            IDictionary<string, EnvVal> env,
            string version = "")
            : base(Core.Constants.EdgeHubModuleName, version, status, restartPolicy, settings, imagePullPolicy, startupOrder, configuration, env)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.DesiredStatus = Preconditions.CheckIsDefined(status);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(RestartPolicy.Always)]
        public override RestartPolicy RestartPolicy { get; }

        public override bool Equals(IModule<DockerConfig> other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // EdgeHub Equal() is similar to base class except, we ignore the version since we don't ever use it.
            return string.Equals(this.Name, other.Name) &&
                string.Equals(this.Type, other.Type) &&
                this.DesiredStatus == other.DesiredStatus &&
                this.Config.Equals(other.Config) &&
                this.RestartPolicy == other.RestartPolicy &&
                this.ImagePullPolicy == other.ImagePullPolicy &&
                this.StartupOrder == other.StartupOrder &&
                this.IsEnvDictionaryEqual(other);
        }
    }
}
