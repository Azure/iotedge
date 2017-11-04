// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
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
        readonly ColumnFamiliesProvider columnFamiliesProvider;
        static object columnFamiliesLock = new object();

        ColumnFamilyStorageRocksDbWrapper(RocksDb db, ColumnFamiliesProvider columnFamiliesProvider)
        {
            this.db = db;
            this.columnFamiliesProvider = columnFamiliesProvider;
        }

        public static ColumnFamilyStorageRocksDbWrapper Create(string path, IEnumerable<string> partitionsList)
        {
            Preconditions.CheckNotNull(partitionsList, nameof(partitionsList));
            Preconditions.CheckNonWhiteSpace(path, nameof(path));

            string dbPath = Path.Combine(path, DbFolderName);
            string columnFamiliesFilePath = Path.Combine(path, ColumnFamiliesFileName);
            var columnFamiliesProvider = new ColumnFamiliesProvider(columnFamiliesFilePath);

            lock (columnFamiliesLock)
            {
                IEnumerable<string> existingColumnFamilies = columnFamiliesProvider.ListColumnFamilies();
                IEnumerable<string> columnFamiliesList = existingColumnFamilies.Union(partitionsList, StringComparer.OrdinalIgnoreCase).ToList();
                var columnFamilies = new ColumnFamilies();
                foreach (string columnFamilyName in columnFamiliesList)
                {
                    columnFamilies.Add(columnFamilyName, new ColumnFamilyOptions());
                }

                columnFamiliesProvider.SetColumnFamilies(columnFamiliesList);
                RocksDb db = RocksDb.Open(Options, dbPath, columnFamilies);
                ColumnFamilyStorageRocksDbWrapper rocksDbWrapper = new ColumnFamilyStorageRocksDbWrapper(db, columnFamiliesProvider);
                return rocksDbWrapper;
            }
        }

        public void Compact(ColumnFamilyHandle cf) => this.db.CompactRange(string.Empty, string.Empty, cf);

        public IEnumerable<string> ListColumnFamilies()
        {
            lock (columnFamiliesLock)
            {
                return this.columnFamiliesProvider.ListColumnFamilies();
            }
        }        

        public ColumnFamilyHandle GetColumnFamily(string columnFamilyName) => this.db.GetColumnFamily(columnFamilyName);

        public ColumnFamilyHandle CreateColumnFamily(ColumnFamilyOptions columnFamilyOptions, string entityName)
        {
            lock (columnFamiliesLock)
            {
                this.columnFamiliesProvider.AddColumnFamily(entityName);
                ColumnFamilyHandle handle = this.db.CreateColumnFamily(columnFamilyOptions, entityName);                
                return handle;
            }
        }        

        public void DropColumnFamily(string columnFamilyName)
        {
            lock (columnFamiliesLock)
            {
                this.columnFamiliesProvider.RemoveColumnFamily(columnFamilyName);
                this.db.DropColumnFamily(columnFamilyName);
            }
        }

        public byte[] Get(byte[] key, ColumnFamilyHandle handle) => this.db.Get(key, handle);

        public void Put(byte[] key, byte[] value, ColumnFamilyHandle handle) => this.db.Put(key, value, handle);

        public void Remove(byte[] key, ColumnFamilyHandle handle) => this.db.Remove(key, handle);

        public Iterator NewIterator(ColumnFamilyHandle handle, ReadOptions readOptions) => this.db.NewIterator(handle, readOptions);

        public Iterator NewIterator(ColumnFamilyHandle handle) => this.db.NewIterator(handle);

        public void Dispose()
        {
            if (!this.isDisposed.GetAndSet(true))
            {
                this.db?.Dispose();
            }
        }

        class ColumnFamiliesProvider
        {
            const string DefaultPartitionName = "default";
            string columnFamiliesFilePath;

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

            public IEnumerable<string> ListColumnFamilies() => (File.Exists(this.columnFamiliesFilePath)) ? File.ReadAllLines(columnFamiliesFilePath).ToList() : new List<string> { DefaultPartitionName };
        }
    }
}
