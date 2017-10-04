// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

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
        readonly KeyLockProvider keyLockProvider;

        public EntityStore(IDbStore dbStore, string entityName, int keyShardCount = 1)
        {
            this.dbStore = Preconditions.CheckNotNull(dbStore, nameof(dbStore));
            this.keyLockProvider = new KeyLockProvider(Preconditions.CheckRange(keyShardCount, 1, nameof(keyShardCount)));
            this.EntityName = Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
        }

        public string EntityName { get; }

        public async Task<Option<TV>> Get(TK key)
        {
            Option<byte[]> valueBytes = await this.dbStore.Get(key.ToBytes());
            Option<TV> value = valueBytes.Map(v => v.FromBytes<TV>());
            return value;
        }

        public async Task Put(TK key, TV value)
        {
            using (await this.keyLockProvider.GetLock(key).LockAsync())
            {
                await this.dbStore.Put(key.ToBytes(), value.ToBytes());
            }
        }

        public virtual Task Remove(TK key)
        {
            return this.dbStore.Remove(key.ToBytes());
        }

        public async Task<bool> Remove(TK key, Func<TV, bool> predicate)
        {
            Preconditions.CheckNotNull(predicate, nameof(predicate));
            using (await this.keyLockProvider.GetLock(key).LockAsync())
            {
                Option<TV> value = await this.Get(key);
                return await value.Filter(v => predicate(v)).Match(
                    async v =>
                    {
                        await this.Remove(key);
                        return true;
                    },
                    () => Task.FromResult(false));
            }
        }

        public Task<bool> Update(TK key, Func<TV, TV> updator)
        {
            Preconditions.CheckNotNull(updator, nameof(updator));
            return this.PutOrUpdate(key, Option.None<TV>(), Option.Some(updator));
        }

        public Task PutOrUpdate(TK key, TV value, Func<TV, TV> updator)
        {
            Preconditions.CheckNotNull(updator, nameof(updator));
            return this.PutOrUpdate(key, Option.Some(value), Option.Some(updator));
        }

        public Task FindOrPut(TK key, TV value)
        {
            return this.PutOrUpdate(key, Option.Some(value), Option.None<Func<TV, TV>>());
        }

        async Task<bool> PutOrUpdate(TK key, Option<TV> value, Option<Func<TV, TV>> updator)
        {
            using (await this.keyLockProvider.GetLock(key).LockAsync())
            {
                byte[] keyBytes = key.ToBytes();
                Option<byte[]> existingValueBytes = await this.dbStore.Get(keyBytes);
                return await existingValueBytes.Match(
                    async evb =>
                    {
                        return await updator.Match(
                            async u =>
                            {
                                var existingValue = evb.FromBytes<TV>();
                                TV updatedValue = u(existingValue);
                                await this.dbStore.Put(keyBytes, updatedValue.ToBytes());
                                return true;
                            },
                            () => Task.FromResult(false));
                    },
                    async () =>
                    {
                        return await value.Match(
                            async v =>
                            {
                                await this.dbStore.Put(keyBytes, v.ToBytes());
                                return true;
                            },
                            () => Task.FromResult(false));
                    });
            }
        }

        public async Task<Option<(TK key, TV value)>> GetFirstEntry()
        {
            Option<(byte[] key, byte[] value)> firstEntry = await this.dbStore.GetFirstEntry();
            return firstEntry.Map(e => (e.key.FromBytes<TK>(), e.value.FromBytes<TV>()));
        }

        public async Task<Option<(TK key, TV value)>> GetLastEntry()
        {
            Option<(byte[] key, byte[] value)> lastEntry = await this.dbStore.GetLastEntry();
            return lastEntry.Map(e => (e.key.FromBytes<TK>(), e.value.FromBytes<TV>()));
        }

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> callback) => this.IterateBatch(Option.Some(startKey), batchSize, callback);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> callback) => this.IterateBatch(Option.None<TK>(), batchSize, callback);

        Task IterateBatch(Option<TK> startKey, int batchSize, Func<TK, TV, Task> callback)
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
                k => this.dbStore.IterateBatch(k.ToBytes(), batchSize, DeserializingCallback),
                () => this.dbStore.IterateBatch(batchSize, DeserializingCallback));
        }

        public Task<bool> Contains(TK key) => this.dbStore.Contains(key.ToBytes());

        /// <summary>
        /// Provides locks for keys. Keys are divided into n shards and there is one lock per shard. 
        /// This improves performance as keys from different shards are locked on separate locks
        /// </summary>
        class KeyLockProvider
        {
            readonly AsyncLock[] locks;
            readonly int keyShardCount;

            public KeyLockProvider(int keyShardCount)
            {
                if (keyShardCount <= 0)
                {
                    throw new ArgumentException("KeyShardCount should be > 0");
                }
                this.keyShardCount = keyShardCount;
                this.locks = new AsyncLock[keyShardCount];
                for (int i = 0; i < keyShardCount; i++)
                {
                    this.locks[i] = new AsyncLock();
                }
            }

            public AsyncLock GetLock(TK key)
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }
                int index = Math.Abs(key.GetHashCode() % this.keyShardCount);
                return this.locks[index];
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.dbStore?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
