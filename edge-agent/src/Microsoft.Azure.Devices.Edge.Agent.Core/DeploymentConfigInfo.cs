// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class DeploymentConfigInfo : IEquatable<DeploymentConfigInfo>
    {
        public static DeploymentConfigInfo Empty = new DeploymentConfigInfo(-1, DeploymentConfig.Empty);

        [JsonConstructor]
        public DeploymentConfigInfo(long version, DeploymentConfig deploymentConfig)
        {
            this.Version = Preconditions.CheckRange(version, -1, nameof(version));
            this.DeploymentConfig = Preconditions.CheckNotNull(deploymentConfig, nameof(deploymentConfig));
            this.Exception = Option.None<Exception>();
        }

        public DeploymentConfigInfo(long version, Exception ex)
        {
            this.Version = Preconditions.CheckRange(version, -1, nameof(version));
            this.Exception = Option.Some(Preconditions.CheckNotNull(ex, nameof(ex)));
            this.DeploymentConfig = DeploymentConfig.Empty;
        }

        [JsonProperty("version")]
        public long Version { get; }

        [JsonProperty("deploymentConfig")]
        public DeploymentConfig DeploymentConfig { get; }

        [JsonIgnore]
        public Option<Exception> Exception { get; }

        public bool Equals(DeploymentConfigInfo other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Version == other.Version && Equals(this.DeploymentConfig, other.DeploymentConfig)
                && this.Exception.Equals(other.Exception);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((DeploymentConfigInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Version.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.DeploymentConfig != null ? this.DeploymentConfig.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.Exception.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(DeploymentConfigInfo left, DeploymentConfigInfo right) => Equals(left, right);

        public static bool operator !=(DeploymentConfigInfo left, DeploymentConfigInfo right) => !Equals(left, right);
    }
}
