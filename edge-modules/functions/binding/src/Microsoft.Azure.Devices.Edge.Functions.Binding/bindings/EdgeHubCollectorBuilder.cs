// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding.Bindings
{
    using System;
    using Microsoft.Azure.WebJobs;

    class EdgeHubCollectorBuilder<T> : IConverter<EdgeHubAttribute, IAsyncCollector<T>>
    {
        readonly INameResolver nameResolver;
        const string EdgeHubConnectionString = "EdgeHubConnectionString";

        public EdgeHubCollectorBuilder(INameResolver nameResolver)
        {
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
        }

        public IAsyncCollector<T> Convert(EdgeHubAttribute attribute)
        {
            string connectionString = this.nameResolver.Resolve(EdgeHubConnectionString);
            var client = DeviceClientCache.Instance.GetOrCreate(connectionString);

            return new EdgeHubAsyncCollector<T>(client, attribute);
        }
    }
}
