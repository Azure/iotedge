// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Threading.Tasks;

    // TODO: Replace ITestResultCollection<T> with IAsyncEnumerable<T> when project is moved to .Net Core 3.1
    public interface ITestResultCollection<T> : IDisposable
    {
        T Current { get; }

        Task<bool> MoveNextAsync();

        void Reset();
    }
}
