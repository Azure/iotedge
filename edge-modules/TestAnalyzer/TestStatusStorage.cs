// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class TestStatusStorage
    {
        const string MessageStorePartitionKey = "MessagesCache";
        const string DirectMethodsStorePartitionKey = "DirectMethodsCache";
        const string TwinsStorePartitionKey = "TwinsCache";
        static readonly ILogger Logger = ModuleUtil.CreateLogger("Analyzer");
        ISequentialStore<MessageDetails> messagesStore;
        ISequentialStore<TestOperationResult> directMethodsStore;
        ISequentialStore<TestOperationResult> twinsStore;

        public async Task InitAsync(string storagePath, ISystemEnvironment systemEnvironment, bool optimizeForPerformance)
        {
            StoreProvider storeProvider;
            try
            {
                var partitionsList = new List<string> { "messages", "directMethods", "twins" };
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

            this.messagesStore = await storeProvider.GetSequentialStore<MessageDetails>(MessageStorePartitionKey);
            this.directMethodsStore = await storeProvider.GetSequentialStore<TestOperationResult>(DirectMethodsStorePartitionKey);
            this.twinsStore = await storeProvider.GetSequentialStore<TestOperationResult>(TwinsStorePartitionKey);
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

        public async Task<bool> AddMessageAsync(MessageDetails message)
        {
            await this.messagesStore.Append(message);
            return true;
        }

        public async Task<bool> AddDirectMethodResultAsync(TestOperationResult result)
        {
            await this.directMethodsStore.Append(result);
            return true;
        }

        public async Task<bool> AddTwinResultAsync(TestOperationResult result)
        {
            await this.twinsStore.Append(result);
            return true;
        }

        public async Task ProcessAllMessagesAsync(Action<MessageDetails> callback)
        {
            await this.ProcessAllAsync(this.messagesStore, callback);
        }

        public async Task ProcessAllDirectMethodsAsync(Action<TestOperationResult> callback)
        {
            await this.ProcessAllAsync(this.directMethodsStore, callback);
        }

        public async Task ProcessAllTwinsAsync(Action<TestOperationResult> callback)
        {
            await this.ProcessAllAsync(this.twinsStore, callback);
        }

        private async Task ProcessAllAsync<T>(ISequentialStore<T> store, Action<T> callback)
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
