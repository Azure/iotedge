// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeHubConfig : IEquatable<EdgeHubConfig>
    {
        public EdgeHubConfig(string schemaVersion, IReadOnlyDictionary<string, RouteConfig> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.StoreAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
        }

        public string SchemaVersion { get; }

        public IReadOnlyDictionary<string, RouteConfig> Routes { get; }

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }

        public static bool operator ==(EdgeHubConfig left, EdgeHubConfig right) => Equals(left, right);

        public static bool operator !=(EdgeHubConfig left, EdgeHubConfig right) => !Equals(left, right);

        public bool Equals(EdgeHubConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.SchemaVersion, other.SchemaVersion, StringComparison.OrdinalIgnoreCase)
                   && new ReadOnlyDictionaryComparer<string, RouteConfig>().Equals(this.Routes, other.Routes)
                   && Equals(this.StoreAndForwardConfiguration, other.StoreAndForwardConfiguration);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as EdgeHubConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.SchemaVersion != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(this.SchemaVersion) : 0;
                hashCode = (hashCode * 397) ^ (this.Routes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.StoreAndForwardConfiguration?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}
