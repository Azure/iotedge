// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    public class Route : IEquatable<Route>
    {
        public Route(string id, string condition, string iotHubName, IMessageSource source, Endpoint endpoint, uint priority, uint timeToLiveSecs)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.Condition = Preconditions.CheckNotNull(condition);
            this.IotHubName = Preconditions.CheckNotNull(iotHubName);
            this.Source = source;
            this.Endpoint = Preconditions.CheckNotNull(endpoint);
            this.Priority = priority;
            this.TimeToLiveSecs = timeToLiveSecs;
        }

        public string Id { get; }

        public string Condition { get; }

        public string IotHubName { get; }

        public IMessageSource Source { get; }

        public Endpoint Endpoint { get; }

        public uint Priority { get; }

        public uint TimeToLiveSecs { get; }

        public bool Equals(Route other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Id, other.Id) &&
                   string.Equals(this.Condition, other.Condition) &&
                   this.Source.Equals(other.Source) &&
                   this.Endpoint.Equals(other.Endpoint) &&
                   this.Priority == other.Priority &&
                   this.TimeToLiveSecs == other.TimeToLiveSecs;
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

            return obj.GetType() == this.GetType() && this.Equals((Route)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + this.Id.GetHashCode();
                hash = hash * 31 + this.Condition.GetHashCode();
                hash = hash * 31 + this.Source.GetHashCode();
                hash = hash * 31 + this.Endpoint.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Route(\"{0}\", {1}, \"{2}\" => ({3})", this.Id, this.Source, this.Condition, this.Endpoint);
    }
}
