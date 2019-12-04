// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class TwinEventStorage
    {
        const string DesiredPropertyUpdatePartitionKey = "DesiredPropertyUpdateCache";
        const string DesiredPropertyReceivedPartitionKey = "DesiredPropertyReceivedCache";
        const string ReportedPropertyUpdatePartitionKey = "ReportedPropertyUpdateCache";
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinEventStorage));
        IEntityStore<string, DateTime> desiredPropertyUpdateCache;
        IEntityStore<string, DateTime> desiredPropertyReceivedCache;
        IEntityStore<string, DateTime> reportedPropertyUpdateCache;

        public void Init(string storagePath, ISystemEnvironment systemEnvironment, bool optimizeForPerformance)
        {
            StoreProvider storeProvider;
            try
            {
                var partitionsList = new List<string> { "desiredPropertyUpdated", "desiredPropertyReceived", "reportedPropertyUpdated" };
                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                    new RocksDbOptionsProvider(systemEnvironment, optimizeForPerformance, Option.None<ulong>()),
                    this.GetStoragePath(storagePath),
                    partitionsList);

                storeProvider = new StoreProvider(dbStoreprovider);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            }

            this.desiredPropertyUpdateCache = storeProvider.GetEntityStore<string, DateTime>(DesiredPropertyUpdatePartitionKey);
            this.desiredPropertyReceivedCache = storeProvider.GetEntityStore<string, DateTime>(DesiredPropertyReceivedPartitionKey);
            this.reportedPropertyUpdateCache = storeProvider.GetEntityStore<string, DateTime>(ReportedPropertyUpdatePartitionKey);
        }

        public async Task<bool> AddDesiredPropertyUpdateAsync(string desiredPropertyUpdateId)
        {
            await this.desiredPropertyUpdateCache.Put(desiredPropertyUpdateId, DateTime.UtcNow);
            return true;
        }

        public async Task<bool> AddDesiredPropertyReceivedAsync(string desiredPropertyReceivedId)
        {
            await this.desiredPropertyReceivedCache.Put(desiredPropertyReceivedId, DateTime.UtcNow);
            return true;
        }

        public async Task<bool> AddReportedPropertyUpdateAsync(string reportedPropertyUpdateId)
        {
            await this.reportedPropertyUpdateCache.Put(reportedPropertyUpdateId, DateTime.UtcNow);
            return true;
        }

        public async Task<bool> RemoveDesiredPropertyUpdateAsync(string desiredPropertyUpdateId)
        {
            await this.desiredPropertyUpdateCache.Remove(desiredPropertyUpdateId);
            return true;
        }

        public async Task<bool> RemoveDesiredPropertyReceivedAsync(string desiredPropertyReceivedId)
        {
            await this.desiredPropertyReceivedCache.Remove(desiredPropertyReceivedId);
            return true;
        }

        public async Task<bool> RemoveReportedPropertyUpdateAsync(string reportedPropertyUpdateId)
        {
            await this.reportedPropertyUpdateCache.Remove(reportedPropertyUpdateId);
            return true;
        }

        public async Task<Dictionary<string, DateTime>> GetAllDesiredPropertiesUpdatedAsync()
        {
            return await this.GetAllUpdatesAsync(this.desiredPropertyUpdateCache);
        }

        public async Task<Dictionary<string, DateTime>> GetAllDesiredPropertiesReceivedAsync()
        {
            return await this.GetAllUpdatesAsync(this.desiredPropertyReceivedCache);
        }

        public async Task<Dictionary<string, DateTime>> GetAllReportedPropertiesUpdatedAsync()
        {
            return await this.GetAllUpdatesAsync(this.reportedPropertyUpdateCache);
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

        async Task<Dictionary<string, DateTime>> GetAllUpdatesAsync(IEntityStore<string, DateTime> store)
        {
            Dictionary<string, DateTime> allData = new Dictionary<string, DateTime>();
            await store.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    allData[key] = value;
                    return Task.CompletedTask;
                });
            return allData;
        }
    }
}
