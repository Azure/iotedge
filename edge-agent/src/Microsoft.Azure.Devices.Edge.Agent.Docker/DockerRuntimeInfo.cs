// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DockerRuntimeInfo : IRuntimeInfo<DockerRuntimeConfig>, IEquatable<DockerRuntimeInfo>
    {
        [JsonConstructor]
        public DockerRuntimeInfo(string type, DockerRuntimeConfig config)
        {
            Preconditions.CheckArgument(type?.Equals("docker") ?? false);
            this.Config = config ?? new DockerRuntimeConfig(string.Empty, string.Empty);
        }

        [JsonProperty("type")]
        public string Type => "docker";

        [JsonProperty("settings")]
        public DockerRuntimeConfig Config { get; }

        public override bool Equals(object obj) => this.Equals(obj as DockerRuntimeInfo);

        public bool Equals(IRuntimeInfo other) => this.Equals(other as DockerRuntimeInfo);

        public bool Equals(DockerRuntimeInfo other) =>
            other != null && this.Type == other.Type &&
                   EqualityComparer<DockerRuntimeConfig>.Default.Equals(this.Config, other.Config);

        public override int GetHashCode()
        {
            int hashCode = -466193572;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Type);
            hashCode = hashCode * -1521134295 + EqualityComparer<DockerRuntimeConfig>.Default.GetHashCode(this.Config);
            return hashCode;
        }

        public static bool operator ==(DockerRuntimeInfo info1, DockerRuntimeInfo info2)
        {
            return EqualityComparer<DockerRuntimeInfo>.Default.Equals(info1, info2);
        }

        public static bool operator !=(DockerRuntimeInfo info1, DockerRuntimeInfo info2)
        {
            return !(info1 == info2);
        }
    }
}
