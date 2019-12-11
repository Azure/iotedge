// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
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

    class TestOperationResultStorage
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TestResultCoordinator");
        static Dictionary<string, (string, ISequentialStore<TestOperationResult>)> resultStores;

        public static async Task InitAsync(string storagePath, ISystemEnvironment systemEnvironment, bool optimizeForPerformance, List<ResultSource> resultSources)
        {
            StoreProvider storeProvider;
            try
            {
                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                    new RocksDbOptionsProvider(systemEnvironment, optimizeForPerformance),
                    GetStoragePath(storagePath),
                    resultSources.Select(r => r.Source));

                storeProvider = new StoreProvider(dbStoreprovider);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            }

            resultStores = await InitializeStoresAsync(storeProvider, resultSources);
        }

        static async Task<Dictionary<string, (string, ISequentialStore<TestOperationResult>)>> InitializeStoresAsync(StoreProvider storeProvider, List<ResultSource> resultSources)
        {
            Dictionary<string, (string, ISequentialStore<TestOperationResult>)> resultSourcesToStores = new Dictionary<string, (string, ISequentialStore<TestOperationResult>)>();
            foreach (ResultSource resultSource in resultSources)
            {
                resultSourcesToStores.Add(resultSource.Source, (resultSource.Type, await storeProvider.GetSequentialStore<TestOperationResult>(resultSource.Source)));
            }

            return resultSourcesToStores;
        }

        static string GetStoragePath(string baseStoragePath)
        {
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }

            string storagePath = Path.Combine(baseStoragePath, "TestResultCoordinator");
            Directory.CreateDirectory(storagePath);
            return storagePath;
        }

        public static async Task<bool> AddResultAsync(TestOperationResult testOperationResult)
        {
            (string, ISequentialStore<TestOperationResult>) typeAndResultStore;
            if (resultStores.TryGetValue(testOperationResult.Source, out typeAndResultStore))
            {
                if (string.Equals(testOperationResult.Type, typeAndResultStore.Item1, StringComparison.OrdinalIgnoreCase))
                {
                    await typeAndResultStore.Item2.Append(testOperationResult);
                    return true;
                }
                else
                {
                    string message = $"Result type should be 'messages', 'directMethod', or 'twin'. Current is '{testOperationResult.Type}'.";
                    Logger.LogError(message);
                    throw new InvalidDataException(message);
                }
            }
            else
            {
                string message = $"Source {testOperationResult.Source} is not valid.";
                Logger.LogError(message);
                throw new InvalidDataException(message);
            }
        }
    }
}
