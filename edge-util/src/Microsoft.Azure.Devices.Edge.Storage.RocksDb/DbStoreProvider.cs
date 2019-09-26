// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using RocksDbSharp;

    public class DbStoreProvider : IDbStoreProvider
    {
        const string DefaultPartitionName = "default";
        static readonly TimeSpan CompactionPeriod = TimeSpan.FromHours(2);
        readonly IRocksDbOptionsProvider optionsProvider;
        readonly IRocksDb db;
        readonly ConcurrentDictionary<string, IDbStore> entityDbStoreDictionary;

        readonly Timer compactionTimer; // TODO: Bug logged to be fixed to proper dispose and test.

        DbStoreProvider(IRocksDbOptionsProvider optionsProvider, IRocksDb db, IDictionary<string, IDbStore> entityDbStoreDictionary)
        {
            this.db = db;
            this.optionsProvider = optionsProvider;
            this.entityDbStoreDictionary = new ConcurrentDictionary<string, IDbStore>(entityDbStoreDictionary);
            this.compactionTimer = new Timer(this.RunCompaction, null, CompactionPeriod, CompactionPeriod);
        }

        public static DbStoreProvider Create(
            IRocksDbOptionsProvider optionsProvider,
            string path,
            IEnumerable<string> partitionsList)
        {
            IRocksDb db = RocksDbWrapper.Create(optionsProvider, path, partitionsList);
            IEnumerable<string> columnFamilies = db.ListColumnFamilies();
            IDictionary<string, IDbStore> entityDbStoreDictionary = new Dictionary<string, IDbStore>();
            foreach (string columnFamilyName in columnFamilies)
            {
                ColumnFamilyHandle handle = db.GetColumnFamily(columnFamilyName);
                var dbStorePartition = new ColumnFamilyDbStore(db, handle);
                entityDbStoreDictionary[columnFamilyName] = dbStorePartition;
            }

            var dbStore = new DbStoreProvider(optionsProvider, db, entityDbStoreDictionary);
            return dbStore;
        }

        public IDbStore GetDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            if (!this.entityDbStoreDictionary.TryGetValue(partitionName, out IDbStore entityDbStore))
            {
                ColumnFamilyHandle handle = this.db.CreateColumnFamily(this.optionsProvider.GetColumnFamilyOptions(), partitionName);
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
                    this.db.DropColumnFamily(partitionName);
                }
            }
        }

        public void Close()
        {
            // No-op.
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.db?.Dispose();
                this.compactionTimer?.Dispose();
            }
        }

        void RunCompaction(object state)
        {
            Events.StartingCompaction();
            foreach (KeyValuePair<string, IDbStore> entityDbStore in this.entityDbStoreDictionary)
            {
                if (entityDbStore.Value is ColumnFamilyDbStore cfDbStore)
                {
                    Events.CompactingStore(entityDbStore.Key);
                    this.db.Compact(cfDbStore.Handle);
                }
            }
        }

        static class Events
        {
            // Use an ID not used by other components
            const int IdStart = 4000;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DbStoreProvider>();

            enum EventIds
            {
                StartingCompaction = IdStart,
                StoreCompaction,
            }

            internal static void StartingCompaction()
            {
                Log.LogInformation((int)EventIds.StartingCompaction, "Starting compaction of stores");
            }

            internal static void CompactingStore(string storeName)
            {
                Log.LogInformation((int)EventIds.StoreCompaction, $"Starting compaction of store {storeName}");
            }
        }
    }
}
