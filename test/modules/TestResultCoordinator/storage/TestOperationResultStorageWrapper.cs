// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Storage
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    class TestOperationResultStorageWrapper : ITestOperationResultStorage
    {
        readonly TestOperationResultStorage underlyingStorage;

        public TestOperationResultStorageWrapper(TestOperationResultStorage testOperationResultStorage)
        {
            this.underlyingStorage = Preconditions.CheckNotNull(testOperationResultStorage, nameof(testOperationResultStorage));
        }

        public Task<bool> AddResultAsync(TestOperationResult testOperationResult)
        {
            testOperationResult = StoragePreparer.PrepareTestOperationResult(testOperationResult);
            return this.underlyingStorage.AddResultAsync(testOperationResult);
        }

        public ISequentialStore<TestOperationResult> GetStoreFromSource(string source)
        {
            return this.underlyingStorage.GetStoreFromSource(source);
        }
    }
}
