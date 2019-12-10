// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
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

    class TestOperationResultStorage
    {
        // TODO: sync this result list with the way the rest of TestResultCoordinator does it - once it is finalized
        readonly List<string> resultTypes = new List<string> { "messages", "directMethod", "twin" };
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TestResultCoordinator");
        Dictionary<string, ISequentialStore<TestOperationResult>> resultStores;

        public static TestOperationResultStorage Instance { get; } = new TestOperationResultStorage();

        public TestOperationResultStorage()
        {
        }

        public async Task InitAsync(string storagePath, ISystemEnvironment systemEnvironment, bool optimizeForPerformance)
        {
            StoreProvider storeProvider;
            try
            {
                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                    new RocksDbOptionsProvider(systemEnvironment, optimizeForPerformance),
                    this.GetStoragePath(storagePath),
                    this.resultTypes);

                storeProvider = new StoreProvider(dbStoreprovider);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            }

            this.resultStores = await this.InitializeStoresAsync(storeProvider);
        }

        private async Task<Dictionary<string, ISequentialStore<TestOperationResult>>> InitializeStoresAsync(StoreProvider storeProvider)
        {
            Dictionary<string, ISequentialStore<TestOperationResult>> resultTypesToStores = new Dictionary<string, ISequentialStore<TestOperationResult>>();
            foreach (string resultType in this.resultTypes)
            {
                resultTypesToStores.Add(resultType, await storeProvider.GetSequentialStore<TestOperationResult>(resultType));
            }

            return resultTypesToStores;
        }

        private string GetStoragePath(string baseStoragePath)
        {
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }

            string storagePath = Path.Combine(baseStoragePath, "TestResultCoordinator");
            Directory.CreateDirectory(storagePath);
            return storagePath;
        }

        public async Task<bool> AddTestOperationResultAsync(TestOperationResult testOperationResult)
        {
            ISequentialStore<TestOperationResult> resultStore;
            if (this.resultStores.TryGetValue(testOperationResult.Type, out resultStore))
            {
                await resultStore.Append(testOperationResult);
                return true;
            }
            else
            {
                throw new InvalidDataException($"result type should be 'messages', 'directMethod', or 'twin'. Current is '{testOperationResult.Type}'.");
            }
        }
    }
}
