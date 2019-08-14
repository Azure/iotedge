// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TestRuntimeModule : TestModule, IRuntimeModule<TestConfig>
    {
        public TestRuntimeModule(
            string name,
            string version,
            RestartPolicy restartPolicy,
            string type,
            ModuleStatus desiredStatus,
            TestConfig config,
            int exitCode,
            string statusDescription,
            DateTime lastStartTimeUtc,
            DateTime lastExitTimeUtc,
            int restartCount,
            DateTime lastRestartTimeUtc,
            ModuleStatus runtimeStatus,
            ImagePullPolicy imagePullPolicy = ImagePullPolicy.OnCreate,
            ConfigurationInfo deploymentInfo = null,
            IDictionary<string, EnvVal> env = null)
            : base(name, version, type, desiredStatus, config, restartPolicy, imagePullPolicy, deploymentInfo, env)
        {
            this.ExitCode = exitCode;
            this.StatusDescription = statusDescription;
            this.LastStartTimeUtc = lastStartTimeUtc;
            this.LastExitTimeUtc = lastExitTimeUtc;
            this.RestartCount = restartCount;
            this.LastRestartTimeUtc = lastRestartTimeUtc;
            this.RuntimeStatus = runtimeStatus;
        }

        [JsonProperty(PropertyName = "exitCode")]
        public int ExitCode { get; }

        [JsonProperty(PropertyName = "statusDescription")]
        public string StatusDescription { get; }

        [JsonProperty(PropertyName = "lastStartTime")]
        public DateTime LastStartTimeUtc { get; }

        [JsonProperty(PropertyName = "lastExitTime")]
        public DateTime LastExitTimeUtc { get; }

        [JsonProperty(PropertyName = "restartCount")]
        public int RestartCount { get; }

        [JsonProperty(PropertyName = "lastRestartTime")]
        public DateTime LastRestartTimeUtc { get; }

        [JsonProperty(PropertyName = "runtimeStatus")]
        public ModuleStatus RuntimeStatus { get; }

        public override bool Equals(object obj) => this.Equals(obj as TestModuleBase<TestConfig>);

        public bool Equals(IRuntimeModule other) => this.Equals(other as TestModuleBase<TestConfig>);

        public override bool Equals(IModule<TestConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            // Compare as IModule, then Compare as IReportedModule, if applicable
            if (other is IRuntimeModule)
            {
                var reported = other as IRuntimeModule;
                return base.Equals(other) &&
                       this.ExitCode == reported.ExitCode &&
                       string.Equals(this.StatusDescription, reported.StatusDescription) &&
                       this.LastStartTimeUtc == reported.LastStartTimeUtc &&
                       this.LastExitTimeUtc == reported.LastExitTimeUtc &&
                       this.RestartCount == reported.RestartCount &&
                       this.LastRestartTimeUtc == reported.LastRestartTimeUtc &&
                       this.RuntimeStatus == reported.RuntimeStatus;
            }
            else
            {
                return base.Equals(other);
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

        public IModule WithRuntimeStatus(ModuleStatus newStatus) => new TestRuntimeModule(
            this.Name,
            this.Version,
            this.RestartPolicy,
            this.Type,
            this.DesiredStatus,
            this.Config,
            this.ExitCode,
            this.StatusDescription,
            this.LastStartTimeUtc,
            this.LastExitTimeUtc,
            this.RestartCount,
            this.LastRestartTimeUtc,
            newStatus,
            this.ImagePullPolicy,
            this.ConfigurationInfo);
    }
}
