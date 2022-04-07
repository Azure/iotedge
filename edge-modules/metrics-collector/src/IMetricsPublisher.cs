// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsPublisher
    {
        /// <summary>
        /// Publishes metrics to a source.
        /// </summary>
        /// <param name="metrics">Metrics to publish.</param>
        /// <param name="cancellationToken">Cancels task.</param>
        /// <returns>True if successful, false if unsuccessful and should be retried.</returns>
        Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken);
    }
}
