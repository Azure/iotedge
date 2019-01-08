// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerReportedRuntimeInfo : DockerRuntimeInfo, IEquatable<DockerReportedRuntimeInfo>
    {
        [JsonConstructor]
        public DockerReportedRuntimeInfo(string type, DockerRuntimeConfig config, DockerPlatformInfo platform)
            : base(type, config)
        {
            this.Platform = Preconditions.CheckNotNull(platform);
        }

        [JsonProperty("platform")]
        public DockerPlatformInfo Platform { get; }

        public static bool operator ==(DockerReportedRuntimeInfo info1, DockerReportedRuntimeInfo info2) =>
            EqualityComparer<DockerReportedRuntimeInfo>.Default.Equals(info1, info2);

        public static bool operator !=(DockerReportedRuntimeInfo info1, DockerReportedRuntimeInfo info2) =>
            !(info1 == info2);

        public override bool Equals(object obj) => this.Equals(obj as DockerReportedRuntimeInfo);

        public bool Equals(DockerReportedRuntimeInfo other) =>
            other != null &&
            base.Equals(other) &&
            EqualityComparer<DockerPlatformInfo>.Default.Equals(this.Platform, other.Platform);

        public override int GetHashCode()
        {
            int hashCode = 2079518418;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<DockerPlatformInfo>.Default.GetHashCode(this.Platform);
            return hashCode;
        }
    }
}
