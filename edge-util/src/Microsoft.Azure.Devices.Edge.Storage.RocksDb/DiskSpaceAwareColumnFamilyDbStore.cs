// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.Disk;
    using Microsoft.Azure.Devices.Edge.Util;
    using RocksDbSharp;

    class DiskSpaceAwareColumnFamilyDbStore : ColumnFamilyDbStore
    {
        readonly IStorageSpaceChecker diskSpaceChecker;

        public DiskSpaceAwareColumnFamilyDbStore(IRocksDb db, ColumnFamilyHandle handle, IStorageSpaceChecker diskSpaceChecker)
            : base(db, handle)
        {
            this.diskSpaceChecker = Preconditions.CheckNotNull(diskSpaceChecker, nameof(diskSpaceChecker));
        }

        public override Task Put(byte[] key, byte[] value)
        {
            if (this.diskSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Storage disk is full"));
            }

            return base.Put(key, value);
        }

        public override Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            if (this.diskSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Storage disk is full"));
            }

            return base.Put(key, value, cancellationToken);
        }
    }
}
