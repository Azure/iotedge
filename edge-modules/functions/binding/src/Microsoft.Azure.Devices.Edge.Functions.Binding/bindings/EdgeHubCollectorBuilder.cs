// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding.Bindings
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs;

    class EdgeHubCollectorBuilder : IConverter<EdgeHubAttribute, IAsyncCollector<Message>>
    {
        readonly string connectionString;
        readonly TransportType transportType;

        public EdgeHubCollectorBuilder(string connectionString, TransportType transportType)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.transportType = transportType;
        }

        public IAsyncCollector<Message> Convert(EdgeHubAttribute attribute)
        {
            DeviceClient client = DeviceClientCache.Instance.GetOrCreate(this.connectionString, this.transportType);
            return new EdgeHubAsyncCollector(client, attribute);
        }
    }
}
