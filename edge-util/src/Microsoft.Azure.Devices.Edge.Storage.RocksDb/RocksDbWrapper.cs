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
        static readonly string DbBackupPath = Path.Combine(Temp, "edgehub_rocksdb_backup");
        static readonly ILogger Log = Logger.Factory.CreateLogger<RocksDbWrapper>();

        readonly AtomicBoolean isDisposed = new AtomicBoolean(false);
        readonly RocksDb db;
        readonly string path;
        readonly DbOptions dbOptions;
        readonly Cache cache;
        readonly bool useBackupAndRestore;

        RocksDbWrapper(DbOptions dbOptions, RocksDb db, string path, Cache cache, bool useBackupAndRestore)
        {
            this.db = db;
            this.path = path;
            this.dbOptions = dbOptions;
            this.cache = cache;
            this.useBackupAndRestore = useBackupAndRestore;
        }

        public static RocksDbWrapper Create(IRocksDbOptionsProvider optionsProvider, string path, IEnumerable<string> partitionsList, bool useBackupAndRestore)
        {
            Preconditions.CheckNonWhiteSpace(path, nameof(path));
            Preconditions.CheckNotNull(optionsProvider, nameof(optionsProvider));
            DbOptions dbOptions = Preconditions.CheckNotNull(optionsProvider.GetDbOptions());

            // This is similar to the default cache created by RocksDb if no explicit cache instance is provided as part of
            // initialization.
            Cache lruCache = Cache.CreateLru(8 * 1024 * 1024);
            dbOptions.SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockCache(lruCache));

            if (useBackupAndRestore)
            {
                RestoreDb(dbOptions, path);
            }

            IEnumerable<string> existingColumnFamilies = ListColumnFamilies(dbOptions, path);
            IEnumerable<string> columnFamiliesList = existingColumnFamilies.Union(Preconditions.CheckNotNull(partitionsList, nameof(partitionsList)), StringComparer.OrdinalIgnoreCase).ToList();
            var columnFamilies = new ColumnFamilies();
            foreach (string columnFamilyName in columnFamiliesList)
            {
                columnFamilies.Add(columnFamilyName, optionsProvider.GetColumnFamilyOptions());
            }

            RocksDb db = RocksDb.Open(dbOptions, path, columnFamilies);
            var rocksDbWrapper = new RocksDbWrapper(dbOptions, db, path, lruCache, useBackupAndRestore);
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
            Log.LogInformation($"Dispose called {DbBackupPath}");
            Log.LogInformation("Directory size:" + GetDirectorySize(this.path));
            Log.LogInformation("backup Directory size:" + GetDirectorySize(DbBackupPath));

            if (!this.isDisposed.GetAndSet(true))
            {
                this.GetApproximateMemoryUsage();
                this.BackupDb();
                this.db?.Dispose();
            }
        }

        public ulong GetApproximateMemoryUsage()
        {
            ulong memoryUsedInBytes = 0;
            Log.LogInformation("GetMemoryStats called");
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

        static void RestoreDb(DbOptions dbOptions, string path)
        {
            Log.LogInformation($"Restore called {DbBackupPath}");
            Log.LogInformation("backup Directory sizeon restore:" + GetDirectorySize(DbBackupPath));

            // Backup DB from last backup if available.
            if (Directory.Exists(DbBackupPath))
            {
                Events.RestoringFromBackup();
                IntPtr backupEngine = Native.Instance.rocksdb_backup_engine_open(dbOptions.Handle, DbBackupPath, out IntPtr err);
                Debug.Assert(err == IntPtr.Zero);

                IntPtr restore_options = Native.Instance.rocksdb_restore_options_create();
                Native.Instance.rocksdb_backup_engine_restore_db_from_latest_backup(
                    backupEngine, path, path, restore_options, out err);
                Debug.Assert(err == IntPtr.Zero);

                Native.Instance.rocksdb_restore_options_destroy(restore_options);
                Native.Instance.rocksdb_backup_engine_close(backupEngine);
                Events.RestoreComplete();
            }
            else
            {
                Events.BackupDirectoryNotFound(DbBackupPath);
            }
        }

        void BackupDb()
        {
            if (this.useBackupAndRestore)
            {
                Events.StartingBackup();
                IntPtr backupEngine = Native.Instance.rocksdb_backup_engine_open(this.dbOptions.Handle, DbBackupPath, out IntPtr err);
                Debug.Assert(err == IntPtr.Zero);

                // Create a new DB backup.
                Native.Instance.rocksdb_backup_engine_create_new_backup(backupEngine, this.db.Handle, out err);
                Debug.Assert(err == IntPtr.Zero);

                // Purge old backups but the last one.
                Native.Instance.rocksdb_backup_engine_purge_old_backups(backupEngine, 1, out err);
                Log.LogInformation("Purged old backups: " + err.ToInt64());
                Debug.Assert(err == IntPtr.Zero);

                Log.LogInformation("backup Directory size:" + GetDirectorySize(DbBackupPath));
                Native.Instance.rocksdb_backup_engine_close(backupEngine);
                Events.BackupComplete();
            }
        }

        static long GetDirectorySize(string directoryPath)
        {
            DirectoryInfo directory = new DirectoryInfo(directoryPath);
            return GetDirectorySize(directory);
        }

        static long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;

            // Get size for all files in directory
            FileInfo[] files = directory.GetFiles("*", SearchOption.AllDirectories);
            foreach (FileInfo file in files)
            {
                size += file.Length;
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

        static class Events
        {
            const int IdStart = UtilEventsIds.RocksDb;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RocksDbWrapper>();

            enum EventIds
            {
                StartingBackup = IdStart,
                BackupComplete,
                RestoringFromBackup,
                RestoreComplete,
                BackupDirectoryNotFound
            }

            internal static void StartingBackup()
            {
                Log.LogInformation((int)EventIds.StartingBackup, "Starting backup of database.");
            }

            internal static void BackupComplete()
            {
                Log.LogInformation((int)EventIds.BackupComplete, $"Backup of database complete.");
            }

            internal static void RestoringFromBackup()
            {
                Log.LogInformation((int)EventIds.RestoringFromBackup, "Starting restore of database from last backup.");
            }

            internal static void RestoreComplete()
            {
                Log.LogInformation((int)EventIds.RestoreComplete, "Database restore from backup complete.");
            }

            internal static void BackupDirectoryNotFound(string backupDirectoryPath)
            {
                Log.LogInformation((int)EventIds.BackupDirectoryNotFound, $"The database backup directory {backupDirectoryPath} doesn't exist.");
            }
        }
    }
}
