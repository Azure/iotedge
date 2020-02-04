// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Nito.AsyncEx;

    /// <summary>
    /// Provides a wrapper over a db store that is aware of storage space limits.
    /// </summary>
    class StorageSpaceAwareDbStore : DbStoreDecorator, ISizedDbStore
    {
        readonly IStorageSpaceChecker storageSpaceChecker;
        readonly AsyncLock asyncLock = new AsyncLock();
        long dbSize;

        public StorageSpaceAwareDbStore(IDbStore dbStore, IStorageSpaceChecker diskSpaceChecker)
            : base(dbStore)
        {
            this.storageSpaceChecker = Preconditions.CheckNotNull(diskSpaceChecker, nameof(diskSpaceChecker));
        }

        public long DbSizeInBytes => this.dbSize;

        public new Task Put(byte[] key, byte[] value) => this.Put(key, value, CancellationToken.None);

        public new async Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            using (await this.asyncLock.LockAsync(cancellationToken))
            {
                Option<byte[]> existingValue = await this.Get(key, cancellationToken);
                int sizeIncrease = existingValue.Match(
                    existing =>
                    {
                        return value.Length - existing.Length;
                    },
                    () =>
                    {
                        return key.Length + value.Length;
                    });

                if (sizeIncrease > 0 && this.storageSpaceChecker.IsFull)
                {
                    throw new StorageFullException("Memory store is full");
                }

                this.dbSize += sizeIncrease;
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
