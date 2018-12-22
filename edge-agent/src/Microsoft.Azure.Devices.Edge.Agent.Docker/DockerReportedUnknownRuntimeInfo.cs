// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using CoreConstants = Core.Constants;

    public class DockerReportedUnknownRuntimeInfo : IRuntimeInfo, IEquatable<DockerReportedUnknownRuntimeInfo>
    {
        [JsonConstructor]
        public DockerReportedUnknownRuntimeInfo(DockerPlatformInfo platform)
        {
            this.Platform = Preconditions.CheckNotNull(platform);
        }

        [JsonProperty("platform")]
        public DockerPlatformInfo Platform { get; }

        public string Type => CoreConstants.Unknown;

        public override bool Equals(object obj) => this.Equals(obj as DockerReportedUnknownRuntimeInfo);

        public bool Equals(DockerReportedUnknownRuntimeInfo other) =>
                   other != null &&
                   EqualityComparer<DockerPlatformInfo>.Default.Equals(this.Platform, other.Platform);

        public bool Equals(IRuntimeInfo other) => this.Equals(other as DockerReportedUnknownRuntimeInfo);

        public override int GetHashCode()
        {
            int hashCode = 2079518418;
            hashCode = hashCode * -1521134295;
            hashCode = hashCode * -1521134295 + EqualityComparer<DockerPlatformInfo>.Default.GetHashCode(this.Platform);
            return hashCode;
        }

        public static bool operator ==(DockerReportedUnknownRuntimeInfo info1, DockerReportedUnknownRuntimeInfo info2) =>
            EqualityComparer<DockerReportedUnknownRuntimeInfo>.Default.Equals(info1, info2);

        public static bool operator !=(DockerReportedUnknownRuntimeInfo info1, DockerReportedUnknownRuntimeInfo info2) =>
            !(info1 == info2);
    }
}
