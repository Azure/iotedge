// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerEnvModule : DockerModule, IReportedModule<DockerConfig>
    {
        [JsonProperty(PropertyName = "exitCode")]
        public int ExitCode { get; }

        [JsonProperty(PropertyName = "statusDescription")]
        public string StatusDescription { get; }

        [JsonProperty(PropertyName = "lastStartTime")]
        public string LastStartTime { get; }

        [JsonProperty(PropertyName = "lastExitTime")]
        public string LastExitTime { get; }

        public DockerEnvModule(string name, string version, ModuleStatus status, DockerConfig config, int exitCode, string statusDescription, string lastStartTime, string lastExitTime)
            : base(name, version, status, config)
        {
            this.ExitCode = exitCode;
            this.StatusDescription = statusDescription;
            this.LastExitTime = lastExitTime;
            this.LastStartTime = lastStartTime;
        }

        [JsonConstructor]
        DockerEnvModule(string name, string version, string type, ModuleStatus status, DockerConfig config, int? exitCode, string statusDescription, string lastStartTime, string lastExitTime)
            : this(name, version, status, config, exitCode?? 0, statusDescription, lastStartTime, lastExitTime)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
        }

        public override bool Equals(object obj) => this.Equals(obj as IModule<DockerConfig>);

        public override bool Equals(IModule other) => this.Equals(other as IModule<DockerConfig>);

        public override bool Equals(IModule<DockerConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            // Compare as IModule, then Compare as IReportedModule, if applicable
            if (!base.Equals(other))
            {
                return false;
            }
            else
            {
                var reportedOther = other as IReportedModule;
                if (reportedOther != null)
                {
                    return this.ExitCode == reportedOther.ExitCode &&
                        string.Equals(this.StatusDescription, reportedOther.StatusDescription) &&
                        string.Equals(this.LastStartTime, reportedOther.LastStartTime) &&
                        string.Equals(this.LastExitTime, reportedOther.LastExitTime);
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
                hashCode = (hashCode * 397) ^ (this.StatusDescription != null ? this.StatusDescription.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.LastStartTime != null ? this.LastStartTime.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.LastExitTime != null ? this.LastExitTime.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
