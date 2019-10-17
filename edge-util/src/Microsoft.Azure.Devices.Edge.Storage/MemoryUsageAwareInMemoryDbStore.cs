// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore that is aware of memory usage limits.
    /// </summary>
    class MemoryUsageAwareInMemoryDbStore : InMemoryDbStore
    {
        readonly IStorageSpaceChecker storageSpaceChecker;

        public MemoryUsageAwareInMemoryDbStore(IStorageSpaceChecker diskSpaceChecker)
            : base()
        {
            this.storageSpaceChecker = Preconditions.CheckNotNull(diskSpaceChecker, nameof(diskSpaceChecker));
        }

        public override Task Put(byte[] key, byte[] value)
        {
            if (this.storageSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Memory store is full"));
            }

            return base.Put(key, value);
        }

        public override Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            if (this.storageSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Memory store is full"));
            }

            return base.Put(key, value, cancellationToken);
        }
    }
}
