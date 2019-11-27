// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsScraper
    {
        Task<IEnumerable<Metric>> ScrapeEndpointsAsync(CancellationToken cancellationToken);
    }
}
