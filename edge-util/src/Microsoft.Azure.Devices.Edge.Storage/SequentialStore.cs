// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
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
        const long DefaultHeadOffset = 0;
        readonly IKeyValueStore<byte[], T> entityStore;
        readonly AsyncLock headLockObject = new AsyncLock();
        readonly AsyncLock tailLockObject = new AsyncLock();
        long tailOffset;
        long headOffset;

        SequentialStore(IKeyValueStore<byte[], T> entityStore, string entityName, long headOffset, long tailOffset)
        {
            this.entityStore = entityStore;
            this.headOffset = headOffset;
            this.tailOffset = tailOffset;
            this.EntityName = entityName;
        }

        public string EntityName { get; }

        public static Task<ISequentialStore<T>> Create(IKeyValueStore<byte[], T> entityStore, string entityName)
            => Create(entityStore, entityName, DefaultHeadOffset);

        public static async Task<ISequentialStore<T>> Create(IKeyValueStore<byte[], T> entityStore, string entityName, long defaultHeadOffset)
        {
            Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
            Option<(byte[] key, T value)> firstEntry = await entityStore.GetFirstEntry(CancellationToken.None);
            Option<(byte[] key, T value)> lastEntry = await entityStore.GetLastEntry(CancellationToken.None);
            long MapLocalFunction((byte[] key, T) e) => StoreUtils.GetOffsetFromKey(e.key);
            long headOffset = firstEntry.Map(MapLocalFunction).GetOrElse(defaultHeadOffset);
            long tailOffset = lastEntry.Map(MapLocalFunction).GetOrElse(defaultHeadOffset - 1);
            var sequentialStore = new SequentialStore<T>(entityStore, entityName, headOffset, tailOffset);
            return sequentialStore;
        }

        public Task<long> Append(T item) => this.Append(item, CancellationToken.None);

        public Task<bool> RemoveFirst(Func<long, T, Task<bool>> predicate) => this.RemoveFirst(predicate, CancellationToken.None);

        public Task<IEnumerable<(long, T)>> GetBatch(long startingOffset, int batchSize) => this.GetBatch(startingOffset, batchSize, CancellationToken.None);

        public long GetHeadOffset(CancellationToken cancellationToken)
        {
            return this.headOffset;
        }

        public long GetTailOffset(CancellationToken _)
        {
            return this.tailOffset;
        }

        public async Task<long> Append(T item, CancellationToken cancellationToken)
        {
            using (await this.tailLockObject.LockAsync(cancellationToken))
            {
                long currentOffset = this.tailOffset + 1;
                byte[] key = StoreUtils.GetKeyFromOffset(currentOffset);
                await this.entityStore.Put(key, item, cancellationToken);
                this.tailOffset = currentOffset;
                return currentOffset;
            }
        }

        public async Task<bool> RemoveOffset(Func<long, T, Task<bool>> predicate, long offset, CancellationToken cancellationToken)
        {
            if (offset == this.headOffset)
            {
                return await this.RemoveFirst(predicate, cancellationToken);
            }

            if (this.IsEmpty())
            {
                return false;
            }

            if (offset == this.tailOffset)
            {
                using (await this.tailLockObject.LockAsync(cancellationToken))
                {
                    return await this.RemoveEnd(predicate, cancellationToken, false);
                }
            }

            byte[] key = StoreUtils.GetKeyFromOffset(offset);
            Option<T> value = await this.entityStore.Get(key, cancellationToken);
            bool result = await value
                .Match(
                    async v =>
                    {
                        if (await predicate(offset, v))
                        {
                            await this.entityStore.Remove(key, cancellationToken);
                            return true;
                        }

                        return false;
                    },
                    () => Task.FromResult(false));
            return result;
        }

        public async Task<bool> RemoveFirst(Func<long, T, Task<bool>> predicate, CancellationToken cancellationToken)
        {
            using (await this.headLockObject.LockAsync(cancellationToken))
            {
                // Tail offset could change here, but not holding a lock for efficiency.
                if (this.IsEmpty())
                {
                    return false;
                }

                return await this.RemoveEnd(predicate, cancellationToken, true);
            }
        }

        private async Task<bool> RemoveEnd(Func<long, T, Task<bool>> predicate, CancellationToken cancellationToken, bool head)
        {
            long offset = head ? this.headOffset : this.tailOffset;
            byte[] key = StoreUtils.GetKeyFromOffset(offset);
            Option<T> value = await this.entityStore.Get(key, cancellationToken);
            bool result = await value
                .Match(
                    async v =>
                    {
                        if (await predicate(offset, v))
                        {
                            await this.entityStore.Remove(key, cancellationToken);
                            if (head)
                            {
                                this.headOffset++;
                            }
                            else
                            {
                                this.tailOffset--;
                            }

                            return true;
                        }

                        return false;
                    },
                    () => Task.FromResult(false));
            return result;
        }

        public async Task<IEnumerable<(long, T)>> GetBatch(long startingOffset, int batchSize, CancellationToken cancellationToken)
        {
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));

            if (startingOffset < this.headOffset)
            {
                startingOffset = this.headOffset;
            }

            if (this.IsEmpty() || this.tailOffset < startingOffset)
            {
                return Enumerable.Empty<(long, T)>();
            }

            var batch = new List<(long, T)>();
            byte[] startingKey = StoreUtils.GetKeyFromOffset(startingOffset);
            await this.entityStore.IterateBatch(
                startingKey,
                batchSize,
                (k, v) =>
                {
                    long offsetFromKey = StoreUtils.GetOffsetFromKey(k);
                    batch.Add((offsetFromKey, v));
                    return Task.CompletedTask;
                },
                cancellationToken);
            return batch;
        }

        public Task<ulong> Count() => this.entityStore.Count();

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.entityStore?.Dispose();
            }
        }

        bool IsEmpty() => this.headOffset > this.tailOffset;
    }
}
