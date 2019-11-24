// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsPublisher
    {
        Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken);
    }
}
