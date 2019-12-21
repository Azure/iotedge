// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Storage
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;

    public interface ITestOperationResultStorage
    {
        ISequentialStore<TestOperationResult> GetStoreFromSource(string source);

        Task<bool> AddResultAsync(TestOperationResult testOperationResult);
    }
}
