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
        readonly Task<ISequentialStore<IEnumerable<Metric>>> dataStore;
        readonly HashSet<long> entriesToRemove = new HashSet<long>();

        public MetricsStorage(Task<IStoreProvider> storeProvider)
        {
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            this.dataStore = Task.Run(async () => await (await storeProvider).GetSequentialStore<IEnumerable<Metric>>("Metrics"));
        }

        public async Task StoreMetricsAsync(IEnumerable<Metric> metrics)
        {
            await (await this.dataStore).Append(metrics);
        }

        public async Task<IEnumerable<Metric>> GetAllMetricsAsync()
        {
            return (await (await this.dataStore).GetBatch(0, 1000))
                    .SelectMany(((long offset, IEnumerable<Metric> metrics) storeResult) =>
                    {
                        this.entriesToRemove.Add(storeResult.offset);
                        return storeResult.metrics;
                    });
        }

        public async Task RemoveAllReturnedMetricsAsync()
        {
            while (await (await this.dataStore).RemoveFirst((o, _) => Task.FromResult(this.entriesToRemove.Remove(o))))
            {
            }
        }
    }
}
