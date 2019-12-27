// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TestOperationResultStorage : ITestOperationResultStorage
    {
        static readonly ILogger Logger = Microsoft.Azure.Devices.Edge.ModuleUtil.ModuleUtil.CreateLogger(nameof(TestOperationResultStorage));
        readonly Dictionary<string, ISequentialStore<TestOperationResult>> resultStores;

        TestOperationResultStorage(Dictionary<string, ISequentialStore<TestOperationResult>> resultStores)
        {
            this.resultStores = resultStores;
        }

        public static async Task<TestOperationResultStorage> Create(IStoreProvider storeProvider, List<string> resultSources)
        {
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            var resultSourcesToStores = new Dictionary<string, ISequentialStore<TestOperationResult>>();
            foreach (string resultSource in resultSources)
            {
                resultSourcesToStores.Add(resultSource, await storeProvider.GetSequentialStore<TestOperationResult>(resultSource));
            }

            return new TestOperationResultStorage(resultSourcesToStores);
        }

        public ISequentialStore<TestOperationResult> GetStoreFromSource(string source)
        {
            return this.resultStores.TryGetValue(source, out ISequentialStore<TestOperationResult> store) ? store : throw new InvalidDataException($"Source {source} not found.");
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

                if (!this.resultStores.TryGetValue(testOperationResult.Source, out ISequentialStore<TestOperationResult> resultStore))
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
