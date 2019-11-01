// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class Storage
    {
        const string DesiredPropertyUpdatePartitionKey = "DesiredPropertyUpdateCache";
        const string DesiredPropertyReceivedPartitionKey = "DesiredPropertyReceivedCache";
        const string ReportedPropertyUpdatePartitionKey = "ReportedPropertyUpdateCache";
        static readonly ILogger Log = Logger.Factory.CreateLogger<Storage>();
        ISequentialStore<KeyValuePair<string, DateTime>> desiredPropertyUpdateCache;
        ISequentialStore<KeyValuePair<string, DateTime>> desiredPropertyReceivedCache;
        ISequentialStore<KeyValuePair<string, DateTime>> reportedPropertyUpdateCache;


        public async Task Init(string storagePath, ISystemEnvironment systemEnvironment, bool optimizeForPerformance)
        {
            StoreProvider storeProvider;
            try
            {
                var partitionsList = new List<string> { "messages", "dm" };
                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                    new RocksDbOptionsProvider(systemEnvironment, optimizeForPerformance),
                    this.GetStoragePath(storagePath),
                    partitionsList);

                storeProvider = new StoreProvider(dbStoreprovider);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Log.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            }

            this.desiredPropertyUpdateCache = await storeProvider.GetSequentialStore<KeyValuePair<string, DateTime>>(DesiredPropertyUpdatePartitionKey);
            this.desiredPropertyReceivedCache = await storeProvider.GetSequentialStore<KeyValuePair<string, DateTime>>(DesiredPropertyReceivedPartitionKey);
            this.reportedPropertyUpdateCache = await storeProvider.GetSequentialStore<KeyValuePair<string, DateTime>>(ReportedPropertyUpdatePartitionKey);
        }

        string GetStoragePath(string baseStoragePath)
        {
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }

            string storagePath = Path.Combine(baseStoragePath, "analyzer");
            Directory.CreateDirectory(storagePath);
            return storagePath;
        }

        public async Task<bool> AddDesiredPropertyUpdate(KeyValuePair<string, DateTime> desiredPropertyUpdate)
        {
            await this.desiredPropertyUpdateCache.Append(desiredPropertyUpdate);
            return true;
        }

        public async Task<bool> AddDesiredPropertyReceived(KeyValuePair<string, DateTime> desiredPropertyReceived)
        {
            await this.desiredPropertyReceivedCache.Append(desiredPropertyReceived);
            return true;
        }

        public async Task<bool> AddReportedPropertyUpdate(KeyValuePair<string, DateTime> reportedPropertyUpdate)
        {
            await this.reportedPropertyUpdateCache.Append(reportedPropertyUpdate);
            return true;
        }

        public async Task ProcessAllDesiredPropertiesUpdated(Action<KeyValuePair<string, DateTime>> callback)
        {
            await this.ProcessAllHelper(this.desiredPropertyUpdateCache, callback);
        }

        public async Task ProcessAllDesiredPropertiesReceived(Action<KeyValuePair<string, DateTime>> callback)
        {
            await this.ProcessAllHelper(this.desiredPropertyReceivedCache, callback);
        }

        public async Task ProcessAllReportedPropertiesUpdated(Action<KeyValuePair<string, DateTime>> callback)
        {
            await this.ProcessAllHelper(this.reportedPropertyUpdateCache, callback);
        }

        public async Task ProcessAllHelper<T>(ISequentialStore<T> store, Action<T> callback)
        {
            int batchSize = 10;
            long lastKey = 0;
            var batch = await store.GetBatch(lastKey, batchSize);

            while (batch.Any())
            {
                foreach ((long, T) item in batch)
                {
                    lastKey = item.Item1;
                    callback(item.Item2);
                }

                batch = await store.GetBatch(lastKey + 1, batchSize);
            }
        }
    }
}
