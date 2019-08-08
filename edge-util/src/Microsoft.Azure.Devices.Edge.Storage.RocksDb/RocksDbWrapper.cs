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
    /// </summary>
    sealed class RocksDbWrapper : IRocksDb
    {
        static readonly string Temp = Path.GetTempPath();
        static readonly string DBBackupPath = Path.Combine(Temp, "rocksdb_simple_example_backup");
        static readonly ILogger Log = Logger.Factory.CreateLogger<RocksDbWrapper>();

        readonly AtomicBoolean isDisposed = new AtomicBoolean(false);
        readonly RocksDb db;
        readonly string path;
        readonly DbOptions dbOptions;
        readonly Cache cache;

        RocksDbWrapper(DbOptions dbOptions, RocksDb db, string path, Cache cache)
        {
            this.db = db;
            this.path = path;
            this.dbOptions = dbOptions;
            this.cache = cache;
        }

        public static RocksDbWrapper Create(IRocksDbOptionsProvider optionsProvider, string path, IEnumerable<string> partitionsList)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            Preconditions.CheckNotNull(optionsProvider, nameof(optionsProvider));
            DbOptions dbOptions = Preconditions.CheckNotNull(optionsProvider.GetDbOptions());

            Cache lruCache = Cache.CreateLru(8 * 1024 * 1024);
            dbOptions.SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockCache(lruCache));
            RestoreDb(dbOptions, path);

            IEnumerable<string> existingColumnFamilies = ListColumnFamilies(dbOptions, path);
            IEnumerable<string> columnFamiliesList = existingColumnFamilies.Union(Preconditions.CheckNotNull(partitionsList, nameof(partitionsList)), StringComparer.OrdinalIgnoreCase).ToList();
            var columnFamilies = new ColumnFamilies();
            foreach (string columnFamilyName in columnFamiliesList)
            {
                columnFamilies.Add(columnFamilyName, optionsProvider.GetColumnFamilyOptions());
            }

            RocksDb db = RocksDb.Open(dbOptions, path, columnFamilies);
            var rocksDbWrapper = new RocksDbWrapper(dbOptions, db, path, lruCache);
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
            Log.LogInformation($"Dispose called {DBBackupPath}");
            Log.LogInformation("Directory size:" + GetDirectorySize(this.path));
            Log.LogInformation("backup Directory size:" + GetDirectorySize(DBBackupPath));

            this.GetMemoryStats();
            IntPtr be;
            IntPtr err = IntPtr.Zero;

            // open Backup Engine that we will use for backing up our database
            be = Native.Instance.rocksdb_backup_engine_open(this.dbOptions.Handle, DBBackupPath, out err);
            Log.LogInformation("Backup engine open: " + err.ToInt64());
            Debug.Assert(err == IntPtr.Zero);

            // create new backup in a directory specified by DBBackupPath
            Native.Instance.rocksdb_backup_engine_create_new_backup(be, this.db.Handle, out err);
            Log.LogInformation("Backup engine create backup: " + err.ToInt64());
            Debug.Assert(err == IntPtr.Zero);

            Log.LogInformation("backup Directory size:" + GetDirectorySize(DBBackupPath));

            if (!this.isDisposed.GetAndSet(true))
            {
                this.db?.Dispose();
            }
        }

        private void GetMemoryStats()
        {
            Log.LogInformation("GetMemoryStats called");
            IntPtr mcc;
            mcc = Native.Instance.rocksdb_memory_consumers_create();
            Log.LogInformation("MCC: " + mcc.ToInt64());

            Native.Instance.rocksdb_memory_consumers_add_db(mcc, this.db.Handle);
            Native.Instance.rocksdb_memory_consumers_add_cache(mcc, this.cache.Handle);
            Log.LogInformation("Added db and cache.");

            IntPtr err;
            IntPtr muc = Native.Instance.rocksdb_approximate_memory_usage_create(mcc, out err);
            Log.LogInformation("muc: " + muc.ToInt64());
            Log.LogInformation("err: " + err.ToInt64());

            ulong mtt = Native.Instance.rocksdb_approximate_memory_usage_get_mem_table_total(muc);
            Log.LogInformation("mtt: " + mtt);

            ulong mtrt = Native.Instance.rocksdb_approximate_memory_usage_get_mem_table_readers_total(muc);
            Log.LogInformation("mtrt: " + mtrt);

            ulong cachet = Native.Instance.rocksdb_approximate_memory_usage_get_cache_total(muc);
            Log.LogInformation("cachet: " + cachet);

            Native.Instance.rocksdb_memory_consumers_destroy(mcc);
            Native.Instance.rocksdb_approximate_memory_usage_destroy(muc);
        }

        private static void RestoreDb(DbOptions dbOptions, string path)
        {
            Log.LogInformation($"Restore called {DBBackupPath}");
            Log.LogInformation("backup Directory sizeon restore:" + GetDirectorySize(DBBackupPath));
            IntPtr be;

            // open Backup Engine that we will use for backing up our database
            if (Directory.Exists(DBBackupPath))
            {
                IntPtr err;
                be = Native.Instance.rocksdb_backup_engine_open(dbOptions.Handle, DBBackupPath, out err);
                Log.LogInformation("Backup engine open: " + err.ToInt64());
                Debug.Assert(err == IntPtr.Zero);

                // If something is wrong, you might want to restore data from last backup
                IntPtr restore_options = Native.Instance.rocksdb_restore_options_create();
                Native.Instance.rocksdb_backup_engine_restore_db_from_latest_backup(
                    be, path, path, restore_options, out err);
                Log.LogInformation("Restore result: " + err.ToInt64());
                Debug.Assert(err == IntPtr.Zero);

                Native.Instance.rocksdb_restore_options_destroy(restore_options);

                // cleanup
                Native.Instance.rocksdb_backup_engine_close(be);
            }
            else
            {
                Log.LogInformation($"{DBBackupPath} doesn't exist");
            }
        }

        static long GetDirectorySize(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);
            return GetDirectorySize(directory);
        }

        static long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;

            // Get size for all files in directory
            FileInfo[] files = directory.GetFiles();
            foreach (FileInfo file in files)
            {
                size += file.Length;
            }

            // Recursively get size for all directories in current directory
            DirectoryInfo[] dis = directory.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += GetDirectorySize(di);
            }

            return size;
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
