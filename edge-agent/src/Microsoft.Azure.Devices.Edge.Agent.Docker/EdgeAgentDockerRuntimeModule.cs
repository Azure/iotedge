// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeAgentDockerRuntimeModule : DockerRuntimeModule, IEdgeAgentModule
    {
        [JsonConstructor]
        public EdgeAgentDockerRuntimeModule(
            DockerConfig config,
            ModuleStatus runtimeStatus,
            int exitCode,
            string statusDescription,
            DateTime lastStartTimeUtc,
            DateTime lastExitTime,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configuration,
            IDictionary<string, EnvVal> env,
            string version = "")
            : base(
                Core.Constants.EdgeAgentModuleName,
                version,
                ModuleStatus.Running,
                RestartPolicy.Always,
                config,
                exitCode,
                statusDescription,
                lastStartTimeUtc,
                lastExitTime,
                0,
                DateTime.MinValue,
                runtimeStatus,
                imagePullPolicy,
                configuration,
                env)
        {
            // You maybe wondering why we are setting this here again even though
            // the base class does this assignment. This is due to a behavior
            // in C# where if you have an assignment to a read-only virtual property
            // from a base constructor when it has been overriden in a sub-class, the
            // assignment becomes a no-op.  In order to fix this we need to assign
            // this here again so that the correct property assignment happens for real!
            this.RestartPolicy = RestartPolicy.Always;
            this.DesiredStatus = ModuleStatus.Running;
            this.RestartCount = 0;
            this.LastRestartTimeUtc = DateTime.MinValue;
            this.Version = version ?? string.Empty;
        }

        [JsonIgnore]
        public override string Version { get; }

        [JsonIgnore]
        public override ModuleStatus DesiredStatus { get; }

        [JsonIgnore]
        public override RestartPolicy RestartPolicy { get; }

        [JsonIgnore]
        public override int RestartCount { get; }

        [JsonIgnore]
        public override DateTime LastRestartTimeUtc { get; }

        public override IModule WithRuntimeStatus(ModuleStatus newStatus) => new EdgeAgentDockerRuntimeModule(
            (DockerReportedConfig)this.Config,
            newStatus,
            this.ExitCode,
            this.StatusDescription,
            this.LastStartTimeUtc,
            this.LastExitTimeUtc,
            this.ImagePullPolicy,
            this.ConfigurationInfo,
            this.Env,
            this.Version);
    }
}
