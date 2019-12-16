// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TestOperationResultStorage : ITestOperationResultStorage
    {
        static readonly ILogger Logger = Microsoft.Azure.Devices.Edge.ModuleUtil.ModuleUtil.CreateLogger(nameof(TestOperationResultStorage));
        private Dictionary<string, ISequentialStore<TestOperationResult>> ResultStores { get; set; }

        public static async Task<TestOperationResultStorage> Create(string storagePath, ISystemEnvironment systemEnvironment, bool optimizeForPerformance, List<string> resultSources)
        {
            StoreProvider storeProvider;
            Preconditions.CheckNotNull(systemEnvironment);
            Preconditions.CheckNotNull(optimizeForPerformance);
            Preconditions.CheckNotNull(resultSources);
            try
            {
                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                    new RocksDbOptionsProvider(systemEnvironment, optimizeForPerformance, Option.None<ulong>()),
                    GetStoragePath(storagePath),
                    resultSources);

                storeProvider = new StoreProvider(dbStoreprovider);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            }

            return new TestOperationResultStorage(await InitializeStoresAsync(storeProvider, resultSources));
        }

        private TestOperationResultStorage(Dictionary<string, ISequentialStore<TestOperationResult>> resultStores)
        {
            this.ResultStores = resultStores;
        }

        public ISequentialStore<TestOperationResult> GetStoreFromSource(string source)
        {
            return this.ResultStores.TryGetValue(source, out ISequentialStore<TestOperationResult> store) ? store : throw new InvalidDataException($"Source {source} not found.");
        }

        static async Task<Dictionary<string, ISequentialStore<TestOperationResult>>> InitializeStoresAsync(StoreProvider storeProvider, List<string> resultSources)
        {
            Preconditions.CheckNotNull(storeProvider);
            Preconditions.CheckNotNull(resultSources);
            Dictionary<string, ISequentialStore<TestOperationResult>> resultSourcesToStores = new Dictionary<string, ISequentialStore<TestOperationResult>>();
            foreach (string resultSource in resultSources)
            {
                resultSourcesToStores.Add(resultSource, await storeProvider.GetSequentialStore<TestOperationResult>(resultSource));
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

        public async Task<bool> AddResultAsync(TestOperationResult testOperationResult)
        {
            try
            {
                if (!Enum.TryParse(testOperationResult.Type, out Microsoft.Azure.Devices.Edge.ModuleUtil.TestOperationResultType resultType))
                {
                    Logger.LogWarning($"Test result has unsupported result type '{testOperationResult.Type}'. Test result: {testOperationResult.Source}, {testOperationResult.CreatedAt}, {testOperationResult.Result}");
                    return false;
                }

                if (!this.ResultStores.TryGetValue(testOperationResult.Source, out ISequentialStore<TestOperationResult> resultStore))
                {
                    string message = $"Source {testOperationResult.Source} is not valid.";
                    Logger.LogError(message);
                    return false;
                }

                await resultStore.Append(testOperationResult);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unexpected Exception when adding test result to store");
                throw;
            }
        }
    }
}
