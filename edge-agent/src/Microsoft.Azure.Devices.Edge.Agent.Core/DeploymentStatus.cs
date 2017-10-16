// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class DeploymentStatus : IEquatable<DeploymentStatus>
    {
        public static readonly DeploymentStatus Unknown = new DeploymentStatus(DeploymentStatusCode.Unknown);
        public static readonly DeploymentStatus Success = new DeploymentStatus(DeploymentStatusCode.Successful);

        public DeploymentStatus(DeploymentStatusCode code):
            this(code, string.Empty)
        {
        }

        [JsonConstructor]
        public DeploymentStatus(DeploymentStatusCode code, string description)
        {
            this.Code = code;
            this.Description = description ?? string.Empty;
        }

        [JsonProperty(PropertyName = "code")]
        public DeploymentStatusCode Code { get; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; }

        public override bool Equals(object obj) => this.Equals(obj as DeploymentStatus);

        public bool Equals(DeploymentStatus other)
        {
            return other != null &&
                   this.Code == other.Code &&
                   this.Description == other.Description;
        }

        public override int GetHashCode()
        {
            var hashCode = 1291371069;
            hashCode = hashCode * -1521134295 + this.Code.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Description);
            return hashCode;
        }

        public static bool operator ==(DeploymentStatus status1, DeploymentStatus status2)
        {
            return EqualityComparer<DeploymentStatus>.Default.Equals(status1, status2);
        }

        public static bool operator !=(DeploymentStatus status1, DeploymentStatus status2)
        {
            return !(status1 == status2);
        }

        public DeploymentStatus Clone() => new DeploymentStatus(this.Code, this.Description);
    }
}
