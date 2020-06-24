// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;

    public class RouteConfig : IEquatable<RouteConfig>
    {
        public RouteConfig(string name, string value, Route route)
        {
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
            this.Value = Preconditions.CheckNonWhiteSpace(value, nameof(value));
            this.Route = Preconditions.CheckNotNull(route, nameof(route));
        }

        public string Name { get; }
        public string Value { get; }
        public Route Route { get; }

        public bool Equals(RouteConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Name, other.Name)
                   && string.Equals(this.Value, other.Value)
                   && this.Route.Equals(other.Route);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as RouteConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Name?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (this.Value?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public static bool operator ==(RouteConfig left, RouteConfig right) => Equals(left, right);

        public static bool operator !=(RouteConfig left, RouteConfig right) => !Equals(left, right);
    }
}
