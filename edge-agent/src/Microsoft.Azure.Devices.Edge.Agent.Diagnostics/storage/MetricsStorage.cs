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
        readonly IKeyValueStore<Guid, IEnumerable<Metric>> dataStore;
        readonly List<Guid> entriesToRemove = new List<Guid>();

        public MetricsStorage(IStoreProvider storeProvider)
        {
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            this.dataStore = Preconditions.CheckNotNull(storeProvider.GetEntityStore<Guid, IEnumerable<Metric>>("Metrics"), "dataStore");
        }

        public Task StoreMetricsAsync(IEnumerable<Metric> metrics)
        {
            return this.dataStore.Put(Guid.NewGuid(), metrics);
        }

        public async Task<IEnumerable<Metric>> GetAllMetricsAsync()
        {
            List<IEnumerable<Metric>> result = new List<IEnumerable<Metric>>();
            await this.dataStore.IterateBatch(1000, (guid, metrics) =>
            {
                result.Add(metrics);
                this.entriesToRemove.Add(guid);

                return Task.CompletedTask;
            });

            return result.SelectMany(m => m);
        }

        public Task RemoveAllReturnedMetricsAsync()
        {
            return Task.WhenAll(this.entriesToRemove.Select(this.dataStore.Remove));
        }
    }
}
