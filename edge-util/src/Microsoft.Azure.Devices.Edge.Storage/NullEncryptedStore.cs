// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullEncryptedStore<TK, TV> : IEncryptedStore<TK, TV>
    {
        public void Dispose()
        {
        }

        public Task Put(TK key, TV value) => Task.CompletedTask;

        public Task<Option<TV>> Get(TK key) => Task.FromResult(Option.None<TV>());

        public Task Remove(TK key) => Task.CompletedTask;

        public Task<bool> Contains(TK key) => Task.FromResult(false);

        public Task<Option<(TK key, TV value)>> GetFirstEntry() => Task.FromResult(Option.None<(TK key, TV value)>());

        public Task<Option<(TK key, TV value)>> GetLastEntry() => Task.FromResult(Option.None<(TK key, TV value)>());

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback) => Task.CompletedTask;

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback) => Task.CompletedTask;
    }
}
