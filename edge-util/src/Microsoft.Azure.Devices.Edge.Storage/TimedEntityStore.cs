// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class TimedEntityStore<TK, TV> : TimedKeyValueStore<TK, TV>, IEntityStore<TK, TV>
    {
        readonly IEntityStore<TK, TV> underlyingEntityStore;
        readonly TimeSpan timeout;

        public TimedEntityStore(IEntityStore<TK, TV> underlyingEntityStore, TimeSpan timeout)
            : base(underlyingEntityStore, timeout)
        {
            this.underlyingEntityStore = Preconditions.CheckNotNull(underlyingEntityStore, nameof(underlyingEntityStore));
            this.timeout = timeout;
        }

        public string EntityName => this.underlyingEntityStore.EntityName;

        public Task<bool> Remove(TK key, Func<TV, bool> predicate, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<bool>> containsWithTimeout = cts => this.underlyingEntityStore.Contains(key, cts);
            return containsWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<TV> Update(TK key, Func<TV, TV> updator, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<TV>> containsWithTimeout = cts => this.underlyingEntityStore.Update(key, updator, cts);
            return containsWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<TV> PutOrUpdate(TK key, TV putValue, Func<TV, TV> valueUpdator, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<TV>> containsWithTimeout = cts => this.underlyingEntityStore.PutOrUpdate(key, putValue, valueUpdator, cts);
            return containsWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<TV> FindOrPut(TK key, TV putValue, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<TV>> containsWithTimeout = cts => this.underlyingEntityStore.FindOrPut(key, putValue, cts);
            return containsWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }
    }

    public class TimedKeyValueStore<TK, TV> : IKeyValueStore<TK, TV>

    {
        readonly IKeyValueStore<TK, TV> underlyingKeyValueStore;
        readonly TimeSpan timeout;

        public TimedKeyValueStore(IKeyValueStore<TK, TV> underlyingKeyValueStore, TimeSpan timeout)
        {
            this.underlyingKeyValueStore = Preconditions.CheckNotNull(underlyingKeyValueStore, nameof(underlyingKeyValueStore));
            this.timeout = timeout;
        }

        public void Dispose() => this.underlyingKeyValueStore.Dispose();

        public Task Put(TK key, TV value, CancellationToken cancellationToken)
        {            
            Func<CancellationToken, Task> putWithTimeout = cts => this.underlyingKeyValueStore.Put(key, value, cts);
            return putWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<Option<TV>> Get(TK key, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<Option<TV>>> getWithTimeout = cts => this.underlyingKeyValueStore.Get(key, cts);
            return getWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task Remove(TK key, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task> removeWithTimeout = cts => this.underlyingKeyValueStore.Remove(key, cts);
            return removeWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<bool> Contains(TK key, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<bool>> containsWithTimeout = cts => this.underlyingKeyValueStore.Contains(key, cts);
            return containsWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<Option<(TK key, TV value)>>> getFirstEntryWithTimeout = cts => this.underlyingKeyValueStore.GetFirstEntry(cts);
            return getFirstEntryWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<Option<(TK key, TV value)>>> getLastEntryWithTimeout = cts => this.underlyingKeyValueStore.GetLastEntry(cts);
            return getLastEntryWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task> iterateWithTimeout = cts => this.underlyingKeyValueStore.IterateBatch(batchSize, perEntityCallback, cts);
            return iterateWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task> iterateWithTimeout = cts => this.underlyingKeyValueStore.IterateBatch(startKey, batchSize, perEntityCallback, cts);
            return iterateWithTimeout.ExecuteWithTimeout(cancellationToken, this.timeout);
        }
    }
}
