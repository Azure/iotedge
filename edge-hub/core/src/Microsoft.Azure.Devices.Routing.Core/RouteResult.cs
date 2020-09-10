// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RouteResult : IEquatable<RouteResult>
    {
        public RouteResult(Endpoint endpoint, uint priority, uint timeToLiveSecs)
        {
            this.Endpoint = Preconditions.CheckNotNull(endpoint, nameof(endpoint));
            this.Priority = priority;
            this.TimeToLiveSecs = timeToLiveSecs;
        }

        public Endpoint Endpoint { get; }
        public uint Priority { get; }
        public uint TimeToLiveSecs { get; }

        public bool Equals(RouteResult other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // Check endpoint, priority, and TTL
            bool areEqual =
                this.Endpoint.Equals(other.Endpoint) &&
                this.Priority == other.Priority &&
                this.TimeToLiveSecs == other.TimeToLiveSecs;

            return areEqual;
        }

        public override bool Equals(object obj) => obj.GetType() == this.GetType() && this.Equals((RouteResult)obj);

        public override int GetHashCode()
        {
            // Not accurate to cast from uint to int, but for hashing purposes
            // that's okay, and much better than getting an int overflow
            unchecked
            {
                return this.Endpoint.Id.GetHashCode() + (int)this.Priority + (int)this.TimeToLiveSecs;
            }
        }
    }
}
