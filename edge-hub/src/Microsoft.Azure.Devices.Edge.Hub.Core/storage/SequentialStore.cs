// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
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
        const int DefaultOffset = 0;
        readonly IEntityStore<byte[], T> entityStore;
        readonly AsyncLock lockObject = new AsyncLock();
        long offset;

        SequentialStore(IEntityStore<byte[], T> entityStore, long offset)
        {
            this.entityStore = entityStore;
            this.offset = offset;
        }

        public static async Task<ISequentialStore<T>> Create(IEntityStore<byte[], T> entityStore)
        {
            Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            Option<(byte[] key, T value)> lastEntry = await entityStore.GetLastEntry();
            long offset = lastEntry.Map(e => StoreUtils.GetOffsetFromKey(e.key) + 1).GetOrElse(DefaultOffset);
            var sequentialStore = new SequentialStore<T>(entityStore, offset);
            return sequentialStore;
        }

        public async Task<long> Add(T item)
        {
            using (await this.lockObject.LockAsync())
            {
                long currentOffset = this.offset++;
                byte[] key = StoreUtils.GetKeyFromOffset(currentOffset);
                await this.entityStore.Put(key, item);
                return currentOffset;
            }
        }

        public async Task<IEnumerable<(long, T)>> GetBatch(long startingOffset, int batchSize)
        {
            Preconditions.CheckRange(startingOffset, 0, nameof(startingOffset));
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
                this.lockObject?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
