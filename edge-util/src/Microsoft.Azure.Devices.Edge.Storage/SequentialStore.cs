// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    /// <summary>
    /// Store for storing entities in an ordered list - Entities can be retrieved in the same order in which they were saved.
    /// This can be used for implementing queues. 
    /// Each saved entity is associated with an offset, which can be used to retrieve the entity. 
    /// </summary>
    class SequentialStore<T> : ISequentialStore<T>
    {
        const int DefaultTailOffset = -1;
        const int DefaultHeadOffset = 0;
        readonly IEntityStore<byte[], T> entityStore;
        readonly AsyncLock headLockObject = new AsyncLock();
        readonly AsyncLock tailLockObject = new AsyncLock();
        long tailOffset;
        long headOffset;

        SequentialStore(IEntityStore<byte[], T> entityStore, long headOffset, long tailOffset)
        {
            this.entityStore = entityStore;
            this.headOffset = headOffset;
            this.tailOffset = tailOffset;
        }

        public string EntityName => this.entityStore.EntityName;

        public static async Task<ISequentialStore<T>> Create(IEntityStore<byte[], T> entityStore)
        {
            Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            Option<(byte[] key, T value)> firstEntry = await entityStore.GetFirstEntry();
            Option<(byte[] key, T value)> lastEntry = await entityStore.GetLastEntry();
            long MapLocalFunction((byte[] key, T) e) => StoreUtils.GetOffsetFromKey(e.key);
            long headOffset = firstEntry.Map(MapLocalFunction).GetOrElse(DefaultHeadOffset);
            long tailOffset = lastEntry.Map(MapLocalFunction).GetOrElse(DefaultTailOffset);
            var sequentialStore = new SequentialStore<T>(entityStore, headOffset, tailOffset);
            return sequentialStore;
        }

        public async Task<long> Append(T item)
        {
            using (await this.headLockObject.LockAsync())
            {
                long currentOffset = this.tailOffset + 1;
                byte[] key = StoreUtils.GetKeyFromOffset(currentOffset);
                await this.entityStore.Put(key, item);
                this.tailOffset = currentOffset;
                return currentOffset;
            }
        }

        public async Task<bool> RemoveFirst(Func<long, T, Task<bool>> predicate)
        {
            using (await this.tailLockObject.LockAsync())
            {
                if (this.headOffset > this.tailOffset)
                {
                    return false;
                }

                byte[] key = StoreUtils.GetKeyFromOffset(this.headOffset);
                Option<T> value = await this.entityStore.Get(key);
                bool result = await value
                    .Match(
                        async v =>
                        {
                            if (await predicate(this.headOffset, v))
                            {
                                await this.entityStore.Remove(key);
                                this.headOffset++;
                                return true;
                            }
                            return false;
                        },
                        () => Task.FromResult(false));
                return result;
            }
        }

        public async Task<IEnumerable<(long, T)>> GetBatch(long startingOffset, int batchSize)
        {
            Preconditions.CheckRange(startingOffset, this.headOffset, nameof(startingOffset));
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));

            var batch = new List<(long, T)>();
            byte[] startingKey = StoreUtils.GetKeyFromOffset(startingOffset);
            await this.entityStore.IterateBatch(startingKey, batchSize, (k, v) =>
            {
                long offsetFromKey = StoreUtils.GetOffsetFromKey(k);
                batch.Add((offsetFromKey, v));
                return Task.CompletedTask;
            });
            return batch;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.entityStore?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
