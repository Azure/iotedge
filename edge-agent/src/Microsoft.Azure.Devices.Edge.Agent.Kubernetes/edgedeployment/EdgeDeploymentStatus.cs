// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using Microsoft.Rest;
    using Newtonsoft.Json;

    public class EdgeDeploymentStatus : IEquatable<EdgeDeploymentStatus>
    {
        [JsonProperty(PropertyName = "state")]
        public EdgeDeploymentStatusType State { get; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; }

        public static EdgeDeploymentStatus Success(string message)
            => new EdgeDeploymentStatus(EdgeDeploymentStatusType.Success, message);

        public static EdgeDeploymentStatus Failure(Exception e)
        {
            string message = e is HttpOperationException httpEx
                ? $"{httpEx.Request.Method} [{httpEx.Request.RequestUri}]({httpEx.Message})"
                : e.Message;
            return Failure(message);
        }

        public static EdgeDeploymentStatus Failure(string message)
            => new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, message);

        public static readonly EdgeDeploymentStatus Default = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, string.Empty);

        [JsonConstructor]
        EdgeDeploymentStatus(EdgeDeploymentStatusType state, string message)
        {
            this.State = state;
            this.Message = message;
        }

        public bool Equals(EdgeDeploymentStatus other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.State == other.State && string.Equals(this.Message, other.Message);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
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

            return this.Equals((EdgeDeploymentStatus)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)this.State;
                hashCode = (hashCode * 397) ^ (this.Message != null ? this.Message.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
