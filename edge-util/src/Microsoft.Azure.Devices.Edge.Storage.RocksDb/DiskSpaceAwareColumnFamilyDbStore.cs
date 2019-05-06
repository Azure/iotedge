// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.Disk;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk;
    using Microsoft.Azure.Devices.Edge.Util;

    class DiskSpaceAwareColumnFamilyDbStore : IDbStore
    {
        readonly IDbStore inner;
        readonly IDiskSpaceChecker diskSpaceChecker;

        public DiskSpaceAwareColumnFamilyDbStore(IDbStore inner, IDiskSpaceChecker diskSpaceChecker)
        {
            this.inner = Preconditions.CheckNotNull(inner, nameof(inner));
            this.diskSpaceChecker = Preconditions.CheckNotNull(diskSpaceChecker, nameof(diskSpaceChecker));
        }

        public void Dispose() => this.inner.Dispose();

        public Task Put(byte[] key, byte[] value)
        {
            if (this.diskSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Storage disk is full"));
            }

            return this.inner.Put(key, value);
        }

        public Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            if (this.diskSpaceChecker.IsFull)
            {
                return Task.FromException(new StorageFullException("Storage disk is full"));
            }

            return this.inner.Put(key, value, cancellationToken);
        }

        public Task<Option<byte[]>> Get(byte[] key) => this.inner.Get(key);

        public Task Remove(byte[] key) => this.inner.Remove(key);

        public Task<bool> Contains(byte[] key) => this.inner.Contains(key);

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry() => this.inner.GetFirstEntry();

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry() => this.inner.GetLastEntry();

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback) => this.inner.IterateBatch(batchSize, perEntityCallback);

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback) => this.inner.IterateBatch(startKey, batchSize, perEntityCallback);

        public Task<Option<byte[]>> Get(byte[] key, CancellationToken cancellationToken) => this.inner.Get(key, cancellationToken);

        public Task Remove(byte[] key, CancellationToken cancellationToken) => this.inner.Remove(key, cancellationToken);

        public Task<bool> Contains(byte[] key, CancellationToken cancellationToken) => this.inner.Contains(key, cancellationToken);

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry(CancellationToken cancellationToken) => this.inner.GetFirstEntry(cancellationToken);

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry(CancellationToken cancellationToken) => this.inner.GetLastEntry(cancellationToken);

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback, CancellationToken cancellationToken) => this.inner.IterateBatch(batchSize, perEntityCallback, cancellationToken);

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback, CancellationToken cancellationToken) => this.inner.IterateBatch(startKey, batchSize, perEntityCallback, cancellationToken);
    }
}
