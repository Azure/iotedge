// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Store for particular Key/Value pair. This provides additional functionality on top of Db Key/Value store such as -
    /// 1. FindOrPut/PutOrUpdate support
    /// 2. Serialized writes for the same key
    /// 3. Support for generic types for keys and values
    /// TODO - Since Key/Value types are generic, need to look into the right behavior to handle null values here.
    /// </summary>
    public class EntityStore<TK, TV> : IEntityStore<TK, TV>
    {
        readonly IKeyValueStore<TK, TV> dbStore;
        readonly AsyncLockProvider<TK> keyLockProvider;

        public EntityStore(IKeyValueStore<TK, TV> dbStore, string entityName, int keyShardCount = 1)
        {
            this.dbStore = Preconditions.CheckNotNull(dbStore, nameof(dbStore));
            this.keyLockProvider = new AsyncLockProvider<TK>(Preconditions.CheckRange(keyShardCount, 1, nameof(keyShardCount)));
            this.EntityName = Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
        }

        public string EntityName { get; }

        public Task<Option<TV>> Get(TK key, CancellationToken cancellationToken)
            => this.dbStore.Get(key, cancellationToken);

        public Task Put(TK key, TV value) => this.Put(key, value, CancellationToken.None);

        public Task<Option<TV>> Get(TK key) => this.Get(key, CancellationToken.None);

        public Task Remove(TK key) => this.Remove(key, CancellationToken.None);

        public Task<bool> Contains(TK key) => this.Contains(key, CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetFirstEntry() => this.GetFirstEntry(CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetLastEntry() => this.GetLastEntry(CancellationToken.None);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(batchSize, perEntityCallback, CancellationToken.None);

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(startKey, batchSize, perEntityCallback, CancellationToken.None);

        public async Task Put(TK key, TV value, CancellationToken cancellationToken)
        {
            using (await this.keyLockProvider.GetLock(key).LockAsync(cancellationToken))
            {
                await this.dbStore.Put(key, value, cancellationToken);
            }
        }

        public virtual Task Remove(TK key, CancellationToken cancellationToken)
            => this.dbStore.Remove(key, cancellationToken);

        public Task<bool> Remove(TK key, Func<TV, bool> predicate)
            => this.Remove(key, predicate, CancellationToken.None);

        public async Task<bool> Remove(TK key, Func<TV, bool> predicate, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(predicate, nameof(predicate));
            using (await this.keyLockProvider.GetLock(key).LockAsync(cancellationToken))
            {
                Option<TV> value = await this.Get(key, cancellationToken);
                return await value.Filter(v => predicate(v)).Match(
                    async v =>
                    {
                        await this.Remove(key, cancellationToken);
                        return true;
                    },
                    () => Task.FromResult(false));
            }
        }

        public Task<TV> Update(TK key, Func<TV, TV> updator) =>
            this.Update(key, updator, CancellationToken.None);

        public async Task<TV> Update(TK key, Func<TV, TV> updator, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(updator, nameof(updator));
            using (await this.keyLockProvider.GetLock(key).LockAsync(cancellationToken))
            {
                TV existingValue = (await this.dbStore.Get(key, cancellationToken))
                    .Expect(() => new InvalidOperationException("Value not found in store"));
                TV updatedValue = updator(existingValue);
                await this.dbStore.Put(key, updatedValue, cancellationToken);
                return updatedValue;
            }
        }

        public Task<TV> PutOrUpdate(TK key, TV putValue, Func<TV, TV> valueUpdator) =>
            this.PutOrUpdate(key, putValue, valueUpdator, CancellationToken.None);

        public async Task<TV> PutOrUpdate(TK key, TV value, Func<TV, TV> updator, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(updator, nameof(updator));
            using (await this.keyLockProvider.GetLock(key).LockAsync(cancellationToken))
            {
                byte[] keyBytes = key.ToBytes();
                Option<TV> existingValue = await this.dbStore.Get(key, cancellationToken);
                TV newValue = await existingValue.Map(
                    async e =>
                    {
                        TV updatedValue = updator(e);
                        await this.dbStore.Put(key, updatedValue, cancellationToken);
                        return updatedValue;
                    }).GetOrElse(
                    async () =>
                    {
                        await this.dbStore.Put(key, value, cancellationToken);
                        return value;
                    });
                return newValue;
            }
        }

        public Task<TV> FindOrPut(TK key, TV putValue) =>
            this.FindOrPut(key, putValue, CancellationToken.None);

        public async Task<TV> FindOrPut(TK key, TV value, CancellationToken cancellationToken)
        {
            using (await this.keyLockProvider.GetLock(key).LockAsync(cancellationToken))
            {
                Option<TV> existingValue = await this.dbStore.Get(key, cancellationToken);
                if (!existingValue.HasValue)
                {
                    await this.dbStore.Put(key, value, cancellationToken);
                }

                return existingValue.GetOrElse(value);
            }
        }

        public Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken)
            => this.dbStore.GetFirstEntry(cancellationToken);

        public Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken)
            => this.dbStore.GetLastEntry(cancellationToken);

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
            => this.dbStore.IterateBatch(startKey, batchSize, callback, cancellationToken);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
            => this.dbStore.IterateBatch(batchSize, callback, cancellationToken);

        public Task<bool> Contains(TK key, CancellationToken cancellationToken)
            => this.dbStore.Contains(key, cancellationToken);

        public Task<ulong> Count() => this.dbStore.Count();

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.dbStore?.Dispose();
            }
        }
    }
}
