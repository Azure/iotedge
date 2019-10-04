// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Util;
    using Nito.AsyncEx;
    using ProtoBuf;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore with backup and restore functionality.
    /// </summary>
    class InMemoryDbStoreWithBackupRestore : DbStoreWithBackupRestore
    {
        readonly string entityName;
        //IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore;

        //InMemoryDbStoreWithBackupRestore(string entityName, IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore, IDbStore dbStore)
        InMemoryDbStoreWithBackupRestore(string entityName, IDbStore dbStore)
            : base(dbStore)
        {
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
            //Preconditions.CheckNotNull(itemKeyedCollectionBackupRestore, nameof(itemKeyedCollectionBackupRestore));

            this.entityName = entityName;
            //this.itemKeyedCollectionBackupRestore = itemKeyedCollectionBackupRestore;
        }

        public static async Task<InMemoryDbStoreWithBackupRestore> CreateAsync(string entityName, string backupPath)
        {
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));

            //string backupFileName = HttpUtility.UrlEncode(entityName);
            //string entityBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");
            //IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(entityBackupPath);
            InMemoryDbStoreWithBackupRestore store = new InMemoryDbStoreWithBackupRestore(entityName, new InMemoryDbStore());
            await store.RestoreAsync(backupPath);
            return store;
        }

        async Task RestoreAsync(string backupPath)
        {
            IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(backupPath);
            try
            {
                ItemKeyedCollection items = await itemKeyedCollectionBackupRestore.RestoreAsync(this.entityName);
                foreach (Item item in items)
                {
                    await this.dbStore.Put(item.Key, item.Value);
                }
            }
            catch (IOException exception)
            {
                throw new IOException($"The restore operation for {this.entityName} failed with error.", exception);
            }
        }

        public async Task BackupAsync(string backupPath)
        {
            //string backupFileName = HttpUtility.UrlEncode(this.entityName);
            //string entityBackupPath = Path.Combine(backupPath, $"{backupFileName}.bin");
            IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(backupPath);
            try
            {
                // This is a hack, make it better by not having to create another in-memory collection of items
                // to be backed up.
                ItemKeyedCollection items = new ItemKeyedCollection(new ByteArrayComparer());
                await this.dbStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    items.Add(new Item(key, value));
                    return Task.CompletedTask;
                });

                await itemKeyedCollectionBackupRestore.BackupAsync(this.entityName, items);
            }
            catch (IOException exception)
            {
                throw new IOException($"The backup operation for {this.entityName} failed with error.", exception);
            }
        }

        //public Task Put(byte[] key, byte[] value)
        //{
        //    return ((IDbStore)this.dbStore).Put(key, value);
        //}

        //public Task<Option<byte[]>> Get(byte[] key)
        //{
        //    return ((IDbStore)this.dbStore).Get(key);
        //}

        //public Task Remove(byte[] key)
        //{
        //    return ((IDbStore)this.dbStore).Remove(key);
        //}

        //public Task<bool> Contains(byte[] key)
        //{
        //    return ((IDbStore)this.dbStore).Contains(key);
        //}

        //public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry()
        //{
        //    return ((IDbStore)this.dbStore).GetFirstEntry();
        //}

        //public Task<Option<(byte[] key, byte[] value)>> GetLastEntry()
        //{
        //    return ((IDbStore)this.dbStore).GetLastEntry();
        //}

        //public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback)
        //{
        //    return ((IDbStore)this.dbStore).IterateBatch(batchSize, perEntityCallback);
        //}

        //public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback)
        //{
        //    return ((IDbStore)this.dbStore).IterateBatch(startKey, batchSize, perEntityCallback);
        //}

        //public Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).Put(key, value, cancellationToken);
        //}

        //public Task<Option<byte[]>> Get(byte[] key, CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).Get(key, cancellationToken);
        //}

        //public Task Remove(byte[] key, CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).Remove(key, cancellationToken);
        //}

        //public Task<bool> Contains(byte[] key, CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).Contains(key, cancellationToken);
        //}

        //public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry(CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).GetFirstEntry(cancellationToken);
        //}

        //public Task<Option<(byte[] key, byte[] value)>> GetLastEntry(CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).GetLastEntry(cancellationToken);
        //}

        //public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback, CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).IterateBatch(batchSize, perEntityCallback, cancellationToken);
        //}

        //public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback, CancellationToken cancellationToken)
        //{
        //    return ((IDbStore)this.dbStore).IterateBatch(startKey, batchSize, perEntityCallback, cancellationToken);
        //}

        //public void Dispose()
        //{
        //    ((IDbStore)this.dbStore).Dispose();
        //}
    }
}
