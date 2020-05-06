// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Stores metrics for later upload.
    /// </summary>
    public interface IMetricsStorage
    {
        // Stores given metrics until the next time GetAllMetricsAsync is called.
        // This can be called multiple times before the metrics are retrieved.
        Task StoreMetricsAsync(IEnumerable<Metric> metrics);

        // Retrieves all metrics stored using StoreMetricsAsync since the last time this was called.
        // TODO: change to IAsyncEnumerable when c# 8 is valid
        Task<IEnumerable<Metric>> GetAllMetricsAsync();

        // Removes all metrics that have previously been returned by GetAllMetricsAsync.
        Task RemoveAllReturnedMetricsAsync();
    }
}
