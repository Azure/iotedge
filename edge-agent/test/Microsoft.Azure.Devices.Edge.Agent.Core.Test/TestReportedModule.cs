// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class TestReportedModule : TestModule, IReportedModule<TestConfig>
    {
        [JsonProperty(PropertyName = "exitCode")]
        public int ExitCode { get; }

        [JsonProperty(PropertyName = "statusDescription")]
        public string StatusDescription { get; }

        [JsonProperty(PropertyName = "lastStartTime")]
        public string LastStartTime { get; }

        [JsonProperty(PropertyName = "lastExitTime")]
        public string LastExitTime { get; }

        public TestReportedModule(string name, string version, string type, ModuleStatus status, TestConfig config, int exitCode, string statusDescription, string lastStartTime, string lastExitTime) : base(name, version, type, status, config)
        {
            this.ExitCode = exitCode;
            this.StatusDescription = statusDescription;
            this.LastStartTime = lastStartTime;
            this.LastExitTime = lastExitTime;
        }
        public override bool Equals(object obj) => this.Equals(obj as TestModuleBase<TestConfig>);

        public bool Equals(IReportedModule other) => this.Equals(other as TestModuleBase<TestConfig>);

        public override bool Equals(IModule<TestConfig> other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            // Compare as IModule, then Compare as IReportedModule, if applicable
            if (other is IReportedModule)
            {
                var reported = other as IReportedModule;
                return base.Equals(other) &&
                    string.Equals(this.StatusDescription, reported.StatusDescription) &&
                    string.Equals(this.LastStartTime, reported.LastStartTime) &&
                    string.Equals(this.LastExitTime, reported.LastExitTime) &&
                    this.ExitCode == reported.ExitCode;
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
                hashCode = (hashCode * 397) ^ (int)this.ExitCode;
                hashCode = (hashCode * 397) ^ (this.StatusDescription != null ? this.StatusDescription.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.LastStartTime != null ? this.LastStartTime.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.LastExitTime != null ? this.LastExitTime.GetHashCode() : 0);
                return hashCode;
            }
        }

    }
}