// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;

    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class Route : IEquatable<Route>
    {
        public Route(string id, string condition, string iotHubName, IMessageSource source, ISet<Endpoint> endpoints)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.Condition = Preconditions.CheckNotNull(condition);
            this.IotHubName = Preconditions.CheckNotNull(iotHubName);
            this.Source = source;
            this.Endpoints = Preconditions.CheckNotNull(endpoints).ToImmutableHashSet();
        }

        public string Condition { get; }

        public ISet<Endpoint> Endpoints { get; }

        public string Id { get; }

        public string IotHubName { get; }

        public IMessageSource Source { get; }

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
                   this.Endpoints.SetEquals(other.Endpoints);
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
                hash = this.Endpoints.Aggregate(hash, (acc, b) => acc * 31 + b.GetHashCode());
                return hash;
            }
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Route(\"{0}\", {1}, \"{2}\" => ({3})", this.Id, this.Source, this.Condition, string.Join(", ", this.Endpoints));
    }
}
