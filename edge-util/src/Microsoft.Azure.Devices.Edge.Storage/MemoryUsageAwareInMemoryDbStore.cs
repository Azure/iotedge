// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Nito.AsyncEx;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore that is aware of memory usage limits.
    /// </summary>
    class MemoryUsageAwareInMemoryDbStore : InMemoryDbStore, ISizedDbStore
    {
        readonly IStorageSpaceChecker storageSpaceChecker;
        readonly AsyncLock asyncLock = new AsyncLock();
        long dbSize;

        public MemoryUsageAwareInMemoryDbStore(IStorageSpaceChecker diskSpaceChecker)
            : base()
        {
            this.storageSpaceChecker = Preconditions.CheckNotNull(diskSpaceChecker, nameof(diskSpaceChecker));
        }

        public long DbSizeInBytes => this.dbSize;

        public new Task Put(byte[] key, byte[] value) => this.Put(key, value, CancellationToken.None);

        public new async Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            if (this.storageSpaceChecker.IsFull)
            {
                throw new StorageFullException("Memory store is full");
            }

            using (await this.asyncLock.LockAsync(cancellationToken))
            {
                Option<byte[]> existingValue = await this.Get(key, cancellationToken);
                existingValue.Match(
                    existing =>
                    {
                        this.dbSize += value.Length - existing.Length;
                        return this.dbSize;
                    },
                    () =>
                    {
                        this.dbSize += key.Length + value.Length;
                        return this.dbSize;
                    });

                await base.Put(key, value, cancellationToken);
            }
        }

        public new Task Remove(byte[] key) => this.Remove(key, CancellationToken.None);

        public new async Task Remove(byte[] key, CancellationToken cancellationToken)
        {
            using (await this.asyncLock.LockAsync(cancellationToken))
            {
                Option<byte[]> value = await this.Get(key, cancellationToken);
                value.ForEach(async x =>
                {
                    this.dbSize -= key.Length + x.Length;
                    await base.Remove(key, cancellationToken);
                });
            }
        }
    }
}
