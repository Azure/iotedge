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
        static readonly DbOptions Options = new DbOptions()
            .SetCreateIfMissing()
            .SetCreateMissingColumnFamilies();

        readonly AtomicBoolean isDisposed = new AtomicBoolean(false);
        readonly RocksDb db;
        readonly string path;

        RocksDbWrapper(RocksDb db, string path)
        {
            this.db = db;
            this.path = path;
        }

        public static RocksDbWrapper Create(string path, IEnumerable<string> partitionsList)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));

            IEnumerable<string> existingColumnFamilies = ListColumnFamilies(path);
            IEnumerable<string> columnFamiliesList = existingColumnFamilies.Union(Preconditions.CheckNotNull(partitionsList, nameof(partitionsList)), StringComparer.OrdinalIgnoreCase).ToList();
            var columnFamilies = new ColumnFamilies();
            foreach (string columnFamilyName in columnFamiliesList)
            {
                columnFamilies.Add(columnFamilyName, new ColumnFamilyOptions());
            }

            RocksDb db = RocksDb.Open(Options, path, columnFamilies);
            var rocksDbWrapper = new RocksDbWrapper(db, path);
            return rocksDbWrapper;
        }

        public void Compact(ColumnFamilyHandle cf) => this.db.CompactRange(string.Empty, string.Empty, cf);

        public IEnumerable<string> ListColumnFamilies() => ListColumnFamilies(this.path);

        static IEnumerable<string> ListColumnFamilies(string path)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            // ListColumnFamilies will throw if the DB doesn't exist yet, so wrap it in a try catch.
            IEnumerable<string> columnFamilies = null;
            try
            {
                columnFamilies = RocksDb.ListColumnFamilies(Options, path);
            }
            catch
            {
                // ignored since ListColumnFamilies will throw if the DB doesn't exist yet.
            }

            return columnFamilies ?? Enumerable.Empty<string>();
        }

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
    }
}
