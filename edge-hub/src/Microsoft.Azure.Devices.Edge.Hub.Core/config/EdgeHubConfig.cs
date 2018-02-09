// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;

    public class EdgeHubConfig
    {
        public EdgeHubConfig(string schemaVersion, IEnumerable<(string Name, string Value, Route Route)> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.StoreAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
        }

        public string SchemaVersion { get; }

        public IEnumerable<(string Name, string Value, Route Route)> Routes { get; }

        public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
    }
}
