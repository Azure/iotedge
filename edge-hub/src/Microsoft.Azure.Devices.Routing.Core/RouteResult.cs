// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public class RouteResult : IEquatable<RouteResult>
    {
        public RouteResult(Endpoint endpoint, uint priority, uint timeToLiveSecs)
        {
            this.Endpoint = endpoint;
            this.Priority = priority;
            this.TimeToLiveSecs = timeToLiveSecs;
        }

        public readonly Endpoint Endpoint;
        public readonly uint Priority;
        public readonly uint TimeToLiveSecs;

        public bool Equals(RouteResult other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            // Check endpoint, priority, and TTL
            bool areEqual =
                this.Endpoint.Equals(other.Endpoint) &&
                this.Priority == other.Priority &&
                this.TimeToLiveSecs == other.TimeToLiveSecs;

            return ReferenceEquals(this, other) || areEqual;
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

            return obj.GetType() == this.GetType() && this.Equals((RouteResult)obj);
        }

        public override int GetHashCode()
        {
            // Not an accurate cast from uint to int, but for hashing purposes
            // we'd rather get negative values than an overflow
            return this.Endpoint.Id.GetHashCode() + unchecked((int)this.Priority) + unchecked((int)this.TimeToLiveSecs);
        }
    }
}
