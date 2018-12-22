// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullKeyValueStore<TK, TV> : IKeyValueStore<TK, TV>
    {
        public void Dispose()
        {
        }

        public Task Put(TK key, TV value) => this.Put(key, value, CancellationToken.None);

        public Task<Option<TV>> Get(TK key) => this.Get(key, CancellationToken.None);

        public Task Remove(TK key) => this.Remove(key, CancellationToken.None);

        public Task<bool> Contains(TK key) => this.Contains(key, CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetFirstEntry() => this.GetFirstEntry(CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetLastEntry() => this.GetLastEntry(CancellationToken.None);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(batchSize, perEntityCallback, CancellationToken.None);

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(startKey, batchSize, perEntityCallback, CancellationToken.None);

        public Task Put(TK key, TV value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Option<TV>> Get(TK key, CancellationToken cancellationToken) => Task.FromResult(Option.None<TV>());

        public Task Remove(TK key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> Contains(TK key, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken) => Task.FromResult(Option.None<(TK key, TV value)>());

        public Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken) => Task.FromResult(Option.None<(TK key, TV value)>());

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
