// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeHubDockerRuntimeModule : DockerRuntimeModule, IEdgeHubModule
    {
        public EdgeHubDockerRuntimeModule(
            string name, string version, ModuleStatus desiredStatus,
            RestartPolicy restartPolicy, DockerConfig config, int exitCode,
            string statusDescription, DateTime lastStartTime,
            DateTime lastExitTime, int restartCount, DateTime lastRestartTime,
            ModuleStatus runtimeStatus, ConfigurationInfo configuration
        )
            : base(name, version, desiredStatus, restartPolicy, config,
                exitCode, statusDescription, lastStartTime, lastExitTime,
                restartCount, lastRestartTime, runtimeStatus, configuration)
        {
            // You maybe wondering why we are setting this here again even though
            // the base class does this assignment. This is due to an obscure issue
            // in C# where if you have an assignment to a read-only virtual property
            // from a base constructor when it has been overriden in a sub-class, the
            // assignment becomes a strange no-op. This seems like a bug or something.
            // At any rate, in order to fix this we need to assign this here again
            // so that the correct property assignment happens for real!
            this.DesiredStatus = Preconditions.CheckIsDefined(desiredStatus);
            this.RestartPolicy = Preconditions.CheckIsDefined(restartPolicy);
        }

        [JsonConstructor]
        EdgeHubDockerRuntimeModule(
            string name, string version, string type, ModuleStatus status,
            RestartPolicy restartPolicy, DockerConfig config, int? exitCode,
            string statusDescription, DateTime lastStartTimeUtc,
            DateTime lastExitTimeUtc, int restartCount,
            DateTime lastRestartTimeUtc, ModuleStatus runtimeStatus,
            ConfigurationInfo configurationInfo
        )
            : this(name, version, status, restartPolicy, config, exitCode ?? 0,
                  statusDescription, lastStartTimeUtc, lastExitTimeUtc,
                  restartCount, lastRestartTimeUtc, runtimeStatus, configurationInfo)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }

        [JsonProperty(Required = Required.Always, PropertyName = "status")]
        public override ModuleStatus DesiredStatus { get; }

        [JsonProperty(
            PropertyName = "restartPolicy",
            Required = Required.DisallowNull,
            DefaultValueHandling = DefaultValueHandling.Populate
        )]
        [DefaultValue(RestartPolicy.Always)]
        public override RestartPolicy RestartPolicy { get; }
    }
}
