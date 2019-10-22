// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
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
        const string MessageStorePartitionKey = "MessagesCache";
        const string DirectMethodsStorePartitionKey = "DmCache";
        const string TwinsStorePartitionKey = "TwinCache";
        static readonly ILogger Log = Logger.Factory.CreateLogger<Storage>();
        ISequentialStore<MessageDetails> messagesStore;
        ISequentialStore<ResponseStatus> dmStore;
        ISequentialStore<ResponseStatus> twinStore;

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

            this.messagesStore = await storeProvider.GetSequentialStore<MessageDetails>(MessageStorePartitionKey);
            this.dmStore = await storeProvider.GetSequentialStore<ResponseStatus>(DirectMethodsStorePartitionKey);
            this.twinStore = await storeProvider.GetSequentialStore<ResponseStatus>(DirectMethodsStorePartitionKey);
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

        public async Task<bool> AddMessage(MessageDetails message)
        {
            await this.messagesStore.Append(message);
            return true;
        }

        public async Task<bool> AddDirectMethod(ResponseStatus dmStatus)
        {
            await this.dmStore.Append(dmStatus);
            return true;
        }

        public async Task<bool> AddTwin(ResponseStatus dmStatus)
        {
            await this.twinStore.Append(dmStatus);
            return true;
        }

        // TODO: reorder
        public async Task ProcessAllMessages(Action<MessageDetails> callback)
        {
            await this.ProcessAllHelper(this.messagesStore, callback);
        }

        public async Task ProcessAllDirectMethods(Action<ResponseStatus> callback)
        {
            await this.ProcessAllHelper(this.dmStore, callback);
        }

        public async Task ProcessAllTwins(Action<ResponseStatus> callback)
        {
            await this.ProcessAllHelper(this.twinStore, callback);
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
