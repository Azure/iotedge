// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerRuntimeModule : DockerModule, IRuntimeModule<DockerConfig>
    {
        public DockerRuntimeModule(
            string name,
            string version,
            ModuleStatus desiredStatus,
            RestartPolicy restartPolicy,
            DockerConfig config,
            int exitCode,
            string statusDescription,
            DateTime lastStartTime,
            DateTime lastExitTime,
            int restartCount,
            DateTime lastRestartTime,
            ModuleStatus runtimeStatus,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configuration,
            IDictionary<string, EnvVal> env)
            : base(name, version, desiredStatus, restartPolicy, config, imagePullPolicy, configuration, env)
        {
            this.ExitCode = exitCode;
            this.StatusDescription = statusDescription;
            this.LastExitTimeUtc = lastExitTime;
            this.LastStartTimeUtc = lastStartTime;
            this.RestartCount = Preconditions.CheckRange(restartCount, 0, nameof(restartCount));
            this.LastRestartTimeUtc = lastRestartTime;
            this.RuntimeStatus = Preconditions.CheckIsDefined(runtimeStatus);
        }

        [JsonConstructor]
        DockerRuntimeModule(
            string name,
            string version,
            string type,
            ModuleStatus status,
            RestartPolicy restartPolicy,
            DockerConfig config,
            int? exitCode,
            string statusDescription,
            DateTime lastStartTimeUtc,
            DateTime lastExitTimeUtc,
            int restartCount,
            DateTime lastRestartTimeUtc,
            ModuleStatus runtimeStatus,
            ImagePullPolicy imagePullPolicy,
            ConfigurationInfo configurationInfo,
            IDictionary<string, EnvVal> env)
            : this(
                name,
                version,
                status,
                restartPolicy,
                config,
                exitCode ?? 0,
                statusDescription,
                lastStartTimeUtc,
                lastExitTimeUtc,
                restartCount,
                lastRestartTimeUtc,
                runtimeStatus,
                imagePullPolicy,
                configurationInfo,
                env)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }

        [JsonProperty(PropertyName = "exitCode")]
        public int ExitCode { get; }

        [JsonProperty(PropertyName = "statusDescription")]
        public string StatusDescription { get; }

        [JsonProperty(PropertyName = "lastStartTimeUtc")]
        public DateTime LastStartTimeUtc { get; }

        [JsonProperty(PropertyName = "lastExitTimeUtc")]
        public DateTime LastExitTimeUtc { get; }

        [JsonProperty(PropertyName = "restartCount")]
        public virtual int RestartCount { get; }

        [JsonProperty(PropertyName = "lastRestartTimeUtc")]
        public virtual DateTime LastRestartTimeUtc { get; }

        [JsonProperty(PropertyName = "runtimeStatus")]
        public ModuleStatus RuntimeStatus { get; }

        public override bool Equals(object obj) => this.Equals(obj as IModule<DockerConfig>);

        public override bool Equals(IModule other) => this.Equals(other as IModule<DockerConfig>);

        public override bool Equals(IModule<DockerConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            // Compare as IModule, then Compare as IRuntimeModule, if applicable
            if (!base.Equals(other))
            {
                return false;
            }
            else
            {
                if (other is IRuntimeModule reportedOther)
                {
                    return this.ExitCode == reportedOther.ExitCode &&
                           string.Equals(this.StatusDescription, reportedOther.StatusDescription) &&
                           this.LastStartTimeUtc == reportedOther.LastStartTimeUtc &&
                           this.LastExitTimeUtc == reportedOther.LastExitTimeUtc &&
                           this.RestartCount == reportedOther.RestartCount &&
                           this.LastRestartTimeUtc == reportedOther.LastRestartTimeUtc &&
                           this.RuntimeStatus == reportedOther.RuntimeStatus;
                }

                return true;
            }
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.ExitCode;
                hashCode = (hashCode * 397) ^ (this.StatusDescription?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ this.LastStartTimeUtc.GetHashCode();
                hashCode = (hashCode * 397) ^ this.LastExitTimeUtc.GetHashCode();
                hashCode = (hashCode * 397) ^ this.RestartCount;
                hashCode = (hashCode * 397) ^ this.LastRestartTimeUtc.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)this.RuntimeStatus;
                return hashCode;
            }
        }

        public virtual IModule WithRuntimeStatus(ModuleStatus newStatus) => new DockerRuntimeModule(
            this.Name,
            this.Version,
            this.DesiredStatus,
            this.RestartPolicy,
            this.Config,
            this.ExitCode,
            this.StatusDescription,
            this.LastStartTimeUtc,
            this.LastExitTimeUtc,
            this.RestartCount,
            this.LastRestartTimeUtc,
            newStatus,
            this.ImagePullPolicy,
            this.ConfigurationInfo,
            this.Env);
    }
}
