// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore that is aware of memory usage limits.
    /// </summary>
    class MemoryUsageAwareInMemoryDbStore : InMemoryDbStore, ISizedDbStore
    {
        readonly IStorageSpaceChecker storageSpaceChecker;
        long dbSize;

        public MemoryUsageAwareInMemoryDbStore(IStorageSpaceChecker diskSpaceChecker)
            : base()
        {
            this.storageSpaceChecker = Preconditions.CheckNotNull(diskSpaceChecker, nameof(diskSpaceChecker));
        }

        public long DbSizeInBytes => this.dbSize;

        public new Task Put(byte[] key, byte[] value)
        {
            if (this.storageSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Memory store is full"));
            }

            Interlocked.Add(ref this.dbSize, key.Length + value.Length);
            return base.Put(key, value);
        }

        public new Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            if (this.storageSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Memory store is full"));
            }

            Interlocked.Add(ref this.dbSize, key.Length + value.Length);
            return base.Put(key, value, cancellationToken);
        }

        public new async Task Remove(byte[] key)
        {
            Option<byte[]> value = await this.Get(key);
            value.ForEach(x =>
            {
                Interlocked.Add(ref this.dbSize, -(key.Length + x.Length));
                base.Remove(key);
            });
        }

        public new async Task Remove(byte[] key, CancellationToken cancellationToken)
        {
            Option<byte[]> value = await this.Get(key, cancellationToken);
            value.ForEach(async x =>
            {
                Interlocked.Add(ref this.dbSize, -(key.Length + x.Length));
                await base.Remove(key, cancellationToken);
            });
        }
    }
}
