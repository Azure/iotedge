// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MetricsStorage : IMetricsStorage
    {
        readonly ISequentialStore<IEnumerable<Metric>> dataStore;
        readonly HashSet<long> entriesToRemove = new HashSet<long>();

        public MetricsStorage(ISequentialStore<IEnumerable<Metric>> sequentialStore)
        {
            this.dataStore = Preconditions.CheckNotNull(sequentialStore, nameof(sequentialStore));
        }

        public Task StoreMetricsAsync(IEnumerable<Metric> metrics)
        {
            return this.dataStore.Append(metrics);
        }

        public async Task<IEnumerable<Metric>> GetAllMetricsAsync()
        {
            return (await this.dataStore.GetBatch(0, 1000))
                    .SelectMany(((long offset, IEnumerable<Metric> metrics) storeResult) =>
                    {
                        this.entriesToRemove.Add(storeResult.offset);
                        return storeResult.metrics;
                    });
        }

        public async Task RemoveAllReturnedMetricsAsync()
        {
            bool notLast = false;
            do
            {
                // Only delete entries in entriesToRemove. Also remove from entriesToRemove.
                Task<bool> ShouldRemove(long offset, object _)
                {
                    return Task.FromResult(this.entriesToRemove.Remove(offset));
                }

                notLast = await this.dataStore.RemoveFirst(ShouldRemove);
            }
            while (notLast);
        }
    }
}
