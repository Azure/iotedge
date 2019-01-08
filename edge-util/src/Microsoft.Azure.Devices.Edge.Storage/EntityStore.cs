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
        readonly IDbStore dbStore;
        readonly AsyncLockProvider<TK> keyLockProvider;

        public EntityStore(IDbStore dbStore, string entityName, int keyShardCount = 1)
        {
            this.dbStore = Preconditions.CheckNotNull(dbStore, nameof(dbStore));
            this.keyLockProvider = new AsyncLockProvider<TK>(Preconditions.CheckRange(keyShardCount, 1, nameof(keyShardCount)));
            this.EntityName = Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
        }

        public string EntityName { get; }

        public async Task<Option<TV>> Get(TK key, CancellationToken cancellationToken)
        {
            Option<byte[]> valueBytes = await this.dbStore.Get(key.ToBytes(), cancellationToken);
            Option<TV> value = valueBytes.Map(v => v.FromBytes<TV>());
            return value;
        }

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
                await this.dbStore.Put(key.ToBytes(), value.ToBytes(), cancellationToken);
            }
        }

        public virtual Task Remove(TK key, CancellationToken cancellationToken)
        {
            return this.dbStore.Remove(key.ToBytes(), cancellationToken);
        }

        public Task<bool> Remove(TK key, Func<TV, bool> predicate) =>
            this.Remove(key, predicate, CancellationToken.None);

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
                byte[] keyBytes = key.ToBytes();
                byte[] existingValueBytes = (await this.dbStore.Get(keyBytes, cancellationToken))
                    .Expect(() => new InvalidOperationException("Value not found in store"));
                var existingValue = existingValueBytes.FromBytes<TV>();
                TV updatedValue = updator(existingValue);
                await this.dbStore.Put(keyBytes, updatedValue.ToBytes(), cancellationToken);
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
                Option<byte[]> existingValueBytes = await this.dbStore.Get(keyBytes, cancellationToken);
                TV newValue = await existingValueBytes.Map(
                    async e =>
                    {
                        var existingValue = e.FromBytes<TV>();
                        TV updatedValue = updator(existingValue);
                        await this.dbStore.Put(keyBytes, updatedValue.ToBytes(), cancellationToken);
                        return updatedValue;
                    }).GetOrElse(
                    async () =>
                    {
                        await this.dbStore.Put(keyBytes, value.ToBytes(), cancellationToken);
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
                byte[] keyBytes = key.ToBytes();
                Option<byte[]> existingValueBytes = await this.dbStore.Get(keyBytes, cancellationToken);
                if (!existingValueBytes.HasValue)
                {
                    await this.dbStore.Put(keyBytes, value.ToBytes(), cancellationToken);
                }

                return existingValueBytes.Map(e => e.FromBytes<TV>()).GetOrElse(value);
            }
        }

        public async Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            Option<(byte[] key, byte[] value)> firstEntry = await this.dbStore.GetFirstEntry(cancellationToken);
            return firstEntry.Map(e => (e.key.FromBytes<TK>(), e.value.FromBytes<TV>()));
        }

        public async Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            Option<(byte[] key, byte[] value)> lastEntry = await this.dbStore.GetLastEntry(cancellationToken);
            return lastEntry.Map(e => (e.key.FromBytes<TK>(), e.value.FromBytes<TV>()));
        }

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
            => this.IterateBatch(Option.Some(startKey), batchSize, callback, cancellationToken);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
            => this.IterateBatch(Option.None<TK>(), batchSize, callback, cancellationToken);

        public Task<bool> Contains(TK key, CancellationToken cancellationToken)
            => this.dbStore.Contains(key.ToBytes(), cancellationToken);

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

        Task IterateBatch(Option<TK> startKey, int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Task DeserializingCallback(byte[] keyBytes, byte[] valueBytes)
            {
                var value = valueBytes.FromBytes<TV>();
                var key = keyBytes.FromBytes<TK>();
                return callback(key, value);
            }

            return startKey.Match(
                k => this.dbStore.IterateBatch(k.ToBytes(), batchSize, DeserializingCallback, cancellationToken),
                () => this.dbStore.IterateBatch(batchSize, DeserializingCallback, cancellationToken));
        }
    }
}
