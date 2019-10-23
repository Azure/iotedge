// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsUpload
    {
        Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken);
    }
}
