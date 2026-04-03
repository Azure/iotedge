// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using Microsoft.Azure.Devices.Client;

    class EdgeHubCollectorBuilder : IConverter<EdgeHubAttribute, IAsyncCollector<TelemetryMessage>>
    {
        public IAsyncCollector<TelemetryMessage> Convert(EdgeHubAttribute attribute)
        {
            return new EdgeHubAsyncCollector(attribute);
        }
    }
}
