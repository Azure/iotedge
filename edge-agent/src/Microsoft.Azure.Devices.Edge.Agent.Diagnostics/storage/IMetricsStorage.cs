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
        Task StoreMetricsAsync(IEnumerable<Metric> metrics);

        Task<IEnumerable<Metric>> GetAllMetricsAsync();
    }
}
