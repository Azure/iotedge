// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using RocksDbSharp;

    /// <summary>
    /// Wrapper around RocksDb. This is mainly needed because each ColumnFamilyDbStore contains an instance of this object,
    /// and hence it could get disposed multiple times. This class makes sure the underlying RocksDb instance is disposed only once.
    ///
    /// Because of an issue where ListColumnFamilies does not return an accurate list of column families on Linux,
    /// this class stores the list of column families in a file.
    /// https://github.com/warrenfalk/rocksdb-sharp/issues/32
    /// </summary>
    sealed class ColumnFamilyStorageRocksDbWrapper : IRocksDb
    {
        const string DbFolderName = "db";
        const string ColumnFamiliesFileName = "columnfamilies";

        static readonly DbOptions Options = new DbOptions()
            .SetCreateIfMissing()
            .SetCreateMissingColumnFamilies();

        readonly AtomicBoolean isDisposed = new AtomicBoolean(false);
        readonly RocksDb db;
        readonly Cache cache;
        readonly ColumnFamiliesProvider columnFamiliesProvider;
        static readonly object ColumnFamiliesLock = new object();
        static readonly ILogger Log = Logger.Factory.CreateLogger<ColumnFamilyStorageRocksDbWrapper>();

        ColumnFamilyStorageRocksDbWrapper(RocksDb db, Cache cache, ColumnFamiliesProvider columnFamiliesProvider)
        {
            this.db = db;
            this.cache = cache;
            this.columnFamiliesProvider = columnFamiliesProvider;
        }

        public static ColumnFamilyStorageRocksDbWrapper Create(string path, IEnumerable<string> partitionsList)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));

            string dbPath = Path.Combine(path, DbFolderName);
            string columnFamiliesFilePath = Path.Combine(path, ColumnFamiliesFileName);
            var columnFamiliesProvider = new ColumnFamiliesProvider(columnFamiliesFilePath);

            // This is similar to the default cache created by RocksDb if no explicit cache instance is provided as part of
            // initialization.
            Cache lruCache = Cache.CreateLru(8 * 1024 * 1024);
            Options.SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockCache(lruCache));

            lock (ColumnFamiliesLock)
            {
                IEnumerable<string> existingColumnFamilies = columnFamiliesProvider.ListColumnFamilies();
                IEnumerable<string> columnFamiliesList = existingColumnFamilies.Union(Preconditions.CheckNotNull(partitionsList, nameof(partitionsList)), StringComparer.OrdinalIgnoreCase).ToList();
                var columnFamilies = new ColumnFamilies();
                foreach (string columnFamilyName in columnFamiliesList)
                {
                    columnFamilies.Add(columnFamilyName, new ColumnFamilyOptions());
                }

                columnFamiliesProvider.SetColumnFamilies(columnFamiliesList);
                RocksDb db = RocksDb.Open(Options, dbPath, columnFamilies);
                var rocksDbWrapper = new ColumnFamilyStorageRocksDbWrapper(db, lruCache, columnFamiliesProvider);
                return rocksDbWrapper;
            }
        }

        public void Compact(ColumnFamilyHandle cf) => this.db.CompactRange(string.Empty, string.Empty, cf);

        public IEnumerable<string> ListColumnFamilies()
        {
            lock (ColumnFamiliesLock)
            {
                return this.columnFamiliesProvider.ListColumnFamilies();
            }
        }

        public ColumnFamilyHandle GetColumnFamily(string columnFamilyName) => this.db.GetColumnFamily(columnFamilyName);

        public ColumnFamilyHandle CreateColumnFamily(ColumnFamilyOptions columnFamilyOptions, string entityName)
        {
            lock (ColumnFamiliesLock)
            {
                this.columnFamiliesProvider.AddColumnFamily(entityName);
                ColumnFamilyHandle handle = this.db.CreateColumnFamily(columnFamilyOptions, entityName);
                return handle;
            }
        }

        public void DropColumnFamily(string columnFamilyName)
        {
            lock (ColumnFamiliesLock)
            {
                this.columnFamiliesProvider.RemoveColumnFamily(columnFamilyName);
                this.db.DropColumnFamily(columnFamilyName);
            }
        }

        public byte[] Get(byte[] key, ColumnFamilyHandle handle) => this.db.Get(key, handle);

        public void Put(byte[] key, byte[] value, ColumnFamilyHandle handle) => this.db.Put(key, value, handle);

        public void Remove(byte[] key, ColumnFamilyHandle handle)
        {
            // Work around the remove bug in RocksDbSharp. https://github.com/warrenfalk/rocksdb-sharp/issues/35
            string keyString = key.FromBytes();
            this.db.Remove(keyString, handle);
        }

        public Iterator NewIterator(ColumnFamilyHandle handle, ReadOptions readOptions) => this.db.NewIterator(handle, readOptions);

        public Iterator NewIterator(ColumnFamilyHandle handle) => this.db.NewIterator(handle);

        public void Dispose()
        {
            if (!this.isDisposed.GetAndSet(true))
            {
                this.db?.Dispose();
            }
        }

        public ulong GetApproximateMemoryUsage()
        {
            ulong memoryUsedInBytes = 0;
            Log.LogInformation("ColumnFamilyStorageRocksDbWrapper GetMemoryStats called");
            IntPtr mcc = Native.Instance.rocksdb_memory_consumers_create();

            Native.Instance.rocksdb_memory_consumers_add_db(mcc, this.db.Handle);
            Native.Instance.rocksdb_memory_consumers_add_cache(mcc, this.cache.Handle);

            IntPtr muc = Native.Instance.rocksdb_approximate_memory_usage_create(mcc, out IntPtr err);
            Debug.Assert(err == IntPtr.Zero);

            ulong memTableUsage = Native.Instance.rocksdb_approximate_memory_usage_get_mem_table_total(muc);
            memoryUsedInBytes += memTableUsage;
            Log.LogInformation("mtt: " + memTableUsage);

            ulong memTableReadersUsage = Native.Instance.rocksdb_approximate_memory_usage_get_mem_table_readers_total(muc);
            memoryUsedInBytes += memTableReadersUsage;
            Log.LogInformation("mtrt: " + memTableReadersUsage);

            ulong cacheUsage = Native.Instance.rocksdb_approximate_memory_usage_get_cache_total(muc);
            memoryUsedInBytes += cacheUsage;
            Log.LogInformation("cachet: " + cacheUsage);

            Native.Instance.rocksdb_memory_consumers_destroy(mcc);
            Native.Instance.rocksdb_approximate_memory_usage_destroy(muc);

            return memoryUsedInBytes;
        }

        class ColumnFamiliesProvider
        {
            const string DefaultPartitionName = "default";
            readonly string columnFamiliesFilePath;

            public ColumnFamiliesProvider(string columnFamiliesFilePath)
            {
                this.columnFamiliesFilePath = Preconditions.CheckNonWhiteSpace(columnFamiliesFilePath, nameof(columnFamiliesFilePath));
            }

            public void SetColumnFamilies(IEnumerable<string> columnFamiliesList) => File.WriteAllLines(this.columnFamiliesFilePath, columnFamiliesList);

            public void RemoveColumnFamily(string columnFamilyName)
            {
                List<string> columnFamilies = File.ReadAllLines(this.columnFamiliesFilePath).ToList();
                columnFamilies.Remove(columnFamilyName);
                File.WriteAllLines(this.columnFamiliesFilePath, columnFamilies);
            }

            public void AddColumnFamily(string entityName) => File.AppendAllLines(this.columnFamiliesFilePath, new List<string> { entityName });

            public IEnumerable<string> ListColumnFamilies() => File.Exists(this.columnFamiliesFilePath) ? File.ReadAllLines(this.columnFamiliesFilePath).ToList() : new List<string> { DefaultPartitionName };
        }
    }
}
