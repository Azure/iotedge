// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// A decorating wrapper over a Db store to provide ease of extending DB store behavior.
    /// </summary>
    public abstract class DbStoreDecorator : IDbStore
    {
        protected IDbStore dbStore;

        public DbStoreDecorator(IDbStore dbStore)
        {
            this.dbStore = Preconditions.CheckNotNull(dbStore, nameof(dbStore));
        }

        public Task<bool> Contains(byte[] key)
        {
            return this.dbStore.Contains(key);
        }

        public Task<bool> Contains(byte[] key, CancellationToken cancellationToken)
        {
            return this.dbStore.Contains(key, cancellationToken);
        }

        public void Dispose()
        {
            this.dbStore.Dispose();
        }

        public Task<Option<byte[]>> Get(byte[] key)
        {
            return this.dbStore.Get(key);
        }

        public Task<Option<byte[]>> Get(byte[] key, CancellationToken cancellationToken)
        {
            return this.dbStore.Get(key, cancellationToken);
        }

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry()
        {
            return this.dbStore.GetFirstEntry();
        }

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            return this.dbStore.GetFirstEntry(cancellationToken);
        }

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry()
        {
            return this.dbStore.GetLastEntry();
        }

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            return this.dbStore.GetLastEntry(cancellationToken);
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback)
        {
            return this.dbStore.IterateBatch(batchSize, perEntityCallback);
        }

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback)
        {
            return this.dbStore.IterateBatch(startKey, batchSize, perEntityCallback);
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback, CancellationToken cancellationToken)
        {
            return this.dbStore.IterateBatch(batchSize, perEntityCallback, cancellationToken);
        }

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback, CancellationToken cancellationToken)
        {
            return this.dbStore.IterateBatch(startKey, batchSize, perEntityCallback, cancellationToken);
        }

        public Task Put(byte[] key, byte[] value)
        {
            return this.dbStore.Put(key, value);
        }

        public Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            return this.dbStore.Put(key, value, cancellationToken);
        }

        public Task Remove(byte[] key)
        {
            return this.dbStore.Remove(key);
        }

        public Task Remove(byte[] key, CancellationToken cancellationToken)
        {
            return this.dbStore.Remove(key, cancellationToken);
        }
    }
}
