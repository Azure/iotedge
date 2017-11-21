// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding.Bindings
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs;

    class EdgeHubCollectorBuilder : IConverter<EdgeHubAttribute, IAsyncCollector<Message>>
    {
        readonly INameResolver nameResolver;
        const string EdgeHubConnectionString = "EdgeHubConnectionString";

        public EdgeHubCollectorBuilder(INameResolver nameResolver)
        {
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
        }

        public IAsyncCollector<Message> Convert(EdgeHubAttribute attribute)
        {
            string connectionString = this.nameResolver.Resolve(EdgeHubConnectionString);
            DeviceClient client = DeviceClientCache.Instance.GetOrCreate(connectionString);

            return new EdgeHubAsyncCollector(client, attribute);
        }
    }
}
