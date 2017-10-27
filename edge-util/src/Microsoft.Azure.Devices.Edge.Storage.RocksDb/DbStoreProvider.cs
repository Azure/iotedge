// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using RocksDbSharp;

    public class DbStoreProvider : IDbStoreProvider
    {
        const string DefaultPartitionName = "default";
        readonly IRocksDb db;
        readonly ConcurrentDictionary<string, IDbStore> entityDbStoreDictionary;

        DbStoreProvider(IRocksDb db, IDictionary<string, IDbStore> entityDbStoreDictionary)
        {
            this.db = db;
            this.entityDbStoreDictionary = new ConcurrentDictionary<string, IDbStore>(entityDbStoreDictionary);
        }

        public static DbStoreProvider Create(string path, IEnumerable<string> partitionsList)
        {
            IRocksDb db = ColumnFamilyStorageRocksDbWrapper.Create(path, partitionsList);
            IEnumerable<string> columnFamilies = db.ListColumnFamilies();
            IDictionary<string, IDbStore> entityDbStoreDictionary = new Dictionary<string, IDbStore>();
            foreach (string columnFamilyName in columnFamilies)
            {
                ColumnFamilyHandle handle = db.GetColumnFamily(columnFamilyName);
                var dbStorePartition = new ColumnFamilyDbStore(db, handle);
                entityDbStoreDictionary[columnFamilyName] = dbStorePartition;
            }
            var dbStore = new DbStoreProvider(db, entityDbStoreDictionary);
            return dbStore;
        }

        public IDbStore GetDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            if (!this.entityDbStoreDictionary.TryGetValue(partitionName, out IDbStore entityDbStore))
            {                
                ColumnFamilyHandle handle = this.db.CreateColumnFamily(new ColumnFamilyOptions(), partitionName);
                entityDbStore = new ColumnFamilyDbStore(this.db, handle);
                entityDbStore = this.entityDbStoreDictionary.GetOrAdd(partitionName, entityDbStore);
            }
            return entityDbStore;
        }

        public IDbStore GetDbStore() => this.GetDbStore(DefaultPartitionName);

        public void RemoveDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            // Don't drop the default partition
            if (!partitionName.Equals(DefaultPartitionName, StringComparison.OrdinalIgnoreCase))
            {
                if (this.entityDbStoreDictionary.TryRemove(partitionName, out IDbStore _))
                {
                    // TODO - Check if this deletes the data in the partition. It should as part of compaction.
                    this.db.DropColumnFamily(partitionName);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.db?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
