// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using RocksDbSharp;

    /// <summary>
    /// Wrapper around RocksDb. This is mainly needed because each ColumnFamilyDbStore contains an instance of this object,
    /// and hence it could get disposed multiple times. This class makes sure the underlying RocksDb instance is disposed only once.
    /// </summary>
    sealed class RocksDbWrapper : IRocksDb
    {
        readonly AtomicBoolean isDisposed = new AtomicBoolean(false);
        readonly RocksDb db;
        readonly string path;
        readonly DbOptions dbOptions;

        RocksDbWrapper(DbOptions dbOptions, RocksDb db, string path)
        {
            this.db = db;
            this.path = path;
            this.dbOptions = dbOptions;
        }

        public static RocksDbWrapper Create(IRocksDbOptionsProvider optionsProvider, string path, IEnumerable<string> partitionsList)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            Preconditions.CheckNotNull(optionsProvider, nameof(optionsProvider));
            DbOptions dbOptions = Preconditions.CheckNotNull(optionsProvider.GetDbOptions());
            IEnumerable<string> existingColumnFamilies = ListColumnFamilies(dbOptions, path);
            IEnumerable<string> columnFamiliesList = existingColumnFamilies.Union(Preconditions.CheckNotNull(partitionsList, nameof(partitionsList)), StringComparer.OrdinalIgnoreCase).ToList();
            var columnFamilies = new ColumnFamilies();
            foreach (string columnFamilyName in columnFamiliesList)
            {
                columnFamilies.Add(columnFamilyName, optionsProvider.GetColumnFamilyOptions());
            }

            RocksDb db = RocksDb.Open(dbOptions, path, columnFamilies);
            var rocksDbWrapper = new RocksDbWrapper(dbOptions, db, path);
            return rocksDbWrapper;
        }

        public void Compact(ColumnFamilyHandle cf) => this.db.CompactRange(string.Empty, string.Empty, cf);

        public IEnumerable<string> ListColumnFamilies() => ListColumnFamilies(this.dbOptions, this.path);

        public ColumnFamilyHandle GetColumnFamily(string columnFamilyName) => this.db.GetColumnFamily(columnFamilyName);

        public ColumnFamilyHandle CreateColumnFamily(ColumnFamilyOptions columnFamilyOptions, string entityName) => this.db.CreateColumnFamily(columnFamilyOptions, entityName);

        public void DropColumnFamily(string columnFamilyName) => this.db.DropColumnFamily(columnFamilyName);

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

        static IEnumerable<string> ListColumnFamilies(DbOptions dbOptions, string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            // ListColumnFamilies will throw if the DB doesn't exist yet, so wrap it in a try catch.
            IEnumerable<string> columnFamilies = null;
            try
            {
                columnFamilies = RocksDb.ListColumnFamilies(dbOptions, path);
            }
            catch
            {
                // ignored since ListColumnFamilies will throw if the DB doesn't exist yet.
            }

            return columnFamilies ?? Enumerable.Empty<string>();
        }
    }
}
