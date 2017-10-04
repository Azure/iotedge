// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;

    public class EdgeHubConfig
    {
        public EdgeHubConfig(string schemaVersion, IDictionary<string, Route> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = schemaVersion;
            this.Routes = routes ?? new Dictionary<string, Route>();
            this.StoreAndForwardConfiguration = storeAndForwardConfiguration;
        }

        public string SchemaVersion { get; }

        public IDictionary<string, Route> Routes { get; }

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; private set; }

        internal void ApplyDiff(EdgeHubConfig patch)
        {
            Preconditions.CheckNotNull(patch, nameof(patch));

            if (!string.IsNullOrWhiteSpace(patch.SchemaVersion) &&
                !this.SchemaVersion.Equals(patch.SchemaVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Patch schema version {patch.SchemaVersion} is different from local schema version {this.SchemaVersion}");
            }

            if (patch.Routes != null)
            {                
                foreach (KeyValuePair<string, Route> route in patch.Routes)
                {
                    this.Routes[route.Key] = route.Value;
                }
            }

            if (patch.StoreAndForwardConfiguration != null)
            {
                this.StoreAndForwardConfiguration = patch.StoreAndForwardConfiguration;
            }
        }
    }
}
