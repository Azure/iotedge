// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Wraps backup and restore functionality around a DB Store Provider.
    /// </summary>
    public class DbStoreProviderWithBackupRestore : DbStoreProviderDecorator
    {
        // This dummy value is used as the 'value' for all items/entries in the 'dbStores' dictionary.
        // The intention is to leverage ConcurrentDictionary as a concurrent hash-set.
        const byte DbStoresDummyValue = 0;
        const string BackupMetadataFileName = "meta.json";
        const string DefaultStoreBackupName = "$Default";
        readonly string backupPath;
        readonly ConcurrentDictionary<string, byte> dbStores;
        readonly IDataBackupRestore dataBackupRestore;
        readonly Events events;

        DbStoreProviderWithBackupRestore(
            IDbStoreProvider dbStoreProvider,
            string backupPath,
            IDataBackupRestore dataBackupRestore)
            : base(dbStoreProvider)
        {
            this.backupPath = Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));
            this.dataBackupRestore = Preconditions.CheckNotNull(dataBackupRestore, nameof(dataBackupRestore));
            this.dbStores = new ConcurrentDictionary<string, byte>();
            this.events = new Events(this.Log);
        }

        public static async Task<IDbStoreProvider> CreateAsync(
            IDbStoreProvider dbStoreProvider,
            string backupPath,
            IDataBackupRestore dataBackupRestore)
        {
            DbStoreProviderWithBackupRestore provider = new DbStoreProviderWithBackupRestore(dbStoreProvider, backupPath, dataBackupRestore);
            await provider.RestoreAsync();
            return provider;
        }

        public override IDbStore GetDbStore(string partitionName)
        {
            this.dbStores.GetOrAdd(partitionName, DbStoresDummyValue);
            return base.GetDbStore(partitionName);
        }

        public override IDbStore GetDbStore()
        {
            this.dbStores.GetOrAdd(DefaultStoreBackupName, DbStoresDummyValue);
            return base.GetDbStore();
        }

        public override void RemoveDbStore(string partitionName)
        {
            this.dbStores.TryRemove(partitionName, out _);
            base.RemoveDbStore(partitionName);
        }

        public override void RemoveDbStore()
        {
            this.dbStores.TryRemove(DefaultStoreBackupName, out _);
            base.RemoveDbStore();
        }

        public async override Task CloseAsync()
        {
            await this.BackupAsync();
            await base.CloseAsync();
        }

        async Task RestoreAsync()
        {
            string backupMetadataFilePath = Path.Combine(this.backupPath, BackupMetadataFileName);

            if (!File.Exists(backupMetadataFilePath))
            {
                this.events.NoBackupsForRestore();
                return;
            }

            try
            {
                string fileText = File.ReadAllText(backupMetadataFilePath);
                BackupMetadataList backupMetadataList = JsonConvert.DeserializeObject<BackupMetadataList>(fileText);
                BackupMetadata backupMetadata = backupMetadataList.Backups[0];
                this.events.BackupInformation(backupMetadata.Id, backupMetadata.SerializationFormat, backupMetadata.TimestampUtc, backupMetadata.Stores);

                string latestBackupDirPath = Path.Combine(this.backupPath, backupMetadata.Id.ToString());
                if (Directory.Exists(latestBackupDirPath))
                {
                    this.events.RestoringFromBackup(backupMetadata.Id);
                    foreach (string store in backupMetadata.Stores)
                    {
                        IDbStore dbStore;
                        if (!store.Equals(DefaultStoreBackupName, StringComparison.OrdinalIgnoreCase))
                        {
                            dbStore = this.GetDbStore(store);
                        }
                        else
                        {
                            dbStore = this.GetDbStore();
                        }

                        await this.DbStoreRestoreAsync(store, dbStore, latestBackupDirPath);
                    }

                    this.events.RestoreComplete();
                }
                else
                {
                    this.events.NoBackupsForRestore();
                }
            }
            catch (Exception exception)
            {
                this.events.RestoreFailure($"The restore operation failed with error ${exception}.");

                // Clean up any restored state in the dictionary if the backup fails midway.
                foreach (string store in this.dbStores.Keys.ToList())
                {
                    this.RemoveDbStore(store);
                }

                this.RemoveDbStore();
            }
            finally
            {
                // Delete all other backups as we've either:
                // 1. Restored successfully.
                // 2. Failed during restore which would indicate a bad backup.
                this.CleanupAllBackups(this.backupPath);
            }
        }

        async Task BackupAsync()
        {
            this.events.StartingBackup();
            Guid backupId = Guid.NewGuid();
            string dbBackupDirectory = Path.Combine(this.backupPath, backupId.ToString());

            BackupMetadata newBackupMetadata = new BackupMetadata(backupId, this.dataBackupRestore.DataBackupFormat, DateTime.UtcNow, this.dbStores.Keys.ToList());
            BackupMetadataList backupMetadataList = new BackupMetadataList(new List<BackupMetadata> { newBackupMetadata });
            try
            {
                Directory.CreateDirectory(dbBackupDirectory);

                // Backup other stores.
                foreach (string store in this.dbStores.Keys)
                {
                    IDbStore dbStore = this.dbStoreProvider.GetDbStore(store);
                    await this.DbStoreBackupAsync(store, dbStore, dbBackupDirectory);
                }

                using (StreamWriter file = File.CreateText(Path.Combine(this.backupPath, BackupMetadataFileName)))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, backupMetadataList);
                }

                this.events.BackupComplete();

                // Clean any old backups.
                this.CleanupUnknownBackups(this.backupPath, backupMetadataList);
            }
            catch (Exception exception)
            {
                this.events.BackupFailure($"The backup operation failed with error ${exception}.");

                // Clean up any artifacts of the attempted backup.
                this.CleanupKnownBackups(this.backupPath, backupMetadataList);
            }
        }

        async Task DbStoreRestoreAsync(string store, IDbStore dbStore, string latestBackupDirPath)
        {
            try
            {
                IList<Item> items = await this.dataBackupRestore.RestoreAsync<IList<Item>>(store, latestBackupDirPath);

                if (items != null)
                {
                    foreach (Item item in items)
                    {
                        await dbStore.Put(item.Key, item.Value);
                    }
                }
            }
            catch (IOException exception)
            {
                throw new IOException($"The restore operation for {latestBackupDirPath} failed with error.", exception);
            }
        }

        async Task DbStoreBackupAsync(string store, IDbStore dbStore, string latestBackupDirPath)
        {
            try
            {
                IList<Item> items = new List<Item>();
                await dbStore.IterateBatch(
                    int.MaxValue,
                    (key, value) =>
                    {
                        items.Add(new Item(key, value));
                        return Task.CompletedTask;
                    });

                await this.dataBackupRestore.BackupAsync(store, latestBackupDirPath, items);
            }
            catch (IOException exception)
            {
                throw new IOException($"The backup operation for {store} failed with error.", exception);
            }
        }

        void CleanupAllBackups(string backupPath)
        {
            DirectoryInfo backupDirInfo = new DirectoryInfo(backupPath);
            foreach (FileInfo file in backupDirInfo.GetFiles())
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    this.events.DeletionError(file.FullName, ex);
                }
            }

            foreach (DirectoryInfo dir in backupDirInfo.GetDirectories())
            {
                try
                {
                    dir.Delete(true);
                }
                catch (Exception ex)
                {
                    this.events.DeletionError(dir.FullName, ex);
                }
            }

            this.events.AllBackupsDeleted();
        }

        void CleanupUnknownBackups(string backupPath, BackupMetadataList metadataList)
        {
            DirectoryInfo backupDirInfo = new DirectoryInfo(backupPath);
            HashSet<string> knownBackupDirNames = new HashSet<string>(metadataList.Backups.Select(x => x.Id.ToString()), StringComparer.OrdinalIgnoreCase);
            foreach (DirectoryInfo dir in backupDirInfo.GetDirectories())
            {
                if (!knownBackupDirNames.Contains(dir.Name))
                {
                    try
                    {
                        dir.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        this.events.DeletionError(dir.FullName, ex);
                    }
                }
            }

            this.events.UnknownBackupsDeleted();
        }

        void CleanupKnownBackups(string backupPath, BackupMetadataList metadataList)
        {
            DirectoryInfo backupDirInfo = new DirectoryInfo(backupPath);
            HashSet<string> knownBackupDirNames = new HashSet<string>(metadataList.Backups.Select(x => x.Id.ToString()), StringComparer.OrdinalIgnoreCase);
            foreach (DirectoryInfo dir in backupDirInfo.GetDirectories())
            {
                if (knownBackupDirNames.Contains(dir.Name))
                {
                    try
                    {
                        dir.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        this.events.DeletionError(dir.FullName, ex);
                    }
                }
            }

            this.events.BackupArtifactsCleanedUp();
        }

        class BackupMetadataList
        {
            BackupMetadataList()
            {
            }

            public BackupMetadataList(IList<BackupMetadata> backups)
            {
                this.Backups = backups;
            }

            public IList<BackupMetadata> Backups { get; }
        }

        class BackupMetadata
        {
            BackupMetadata()
            {
            }

            public BackupMetadata(Guid id, SerializationFormat serializationFormat, DateTime timestampUtc, IList<string> stores)
            {
                this.Id = id;
                this.SerializationFormat = serializationFormat;
                this.TimestampUtc = timestampUtc;
                this.Stores = stores;
            }

            public Guid Id { get; }

            [JsonConverter(typeof(StringEnumConverter))]
            public SerializationFormat SerializationFormat { get; set; }

            public IList<string> Stores { get; set; }

            public DateTime TimestampUtc { get; set; }
        }

        class Events
        {
            const int IdStart = UtilEventsIds.DbStoreProviderWithBackupRestore;

            ILogger Log { get; }

            internal Events(ILogger logger)
            {
                this.Log = Preconditions.CheckNotNull(logger, nameof(logger));
            }

            enum EventIds
            {
                StartingBackup = IdStart,
                AllBackupsDeleted,
                BackupArtifactsCleanedUp,
                BackupComplete,
                BackupInformation,
                BackupFailure,
                DeletionError,
                RestoringFromBackup,
                NoBackupsForRestore,
                RestoreComplete,
                RestoreFailure,
                UnknownBackupsDeleted,
            }

            internal void StartingBackup()
            {
                this.Log.LogInformation((int)EventIds.StartingBackup, "Starting backup of database.");
            }

            internal void BackupComplete()
            {
                this.Log.LogInformation((int)EventIds.BackupComplete, $"Backup of database complete.");
            }

            internal void BackupInformation(Guid backupId, SerializationFormat format, DateTime backupTimestamp, IList<string> stores)
            {
                this.Log.LogDebug((int)EventIds.BackupInformation, $"Backup Info: Timestamp={backupTimestamp}, ID={backupId}, Serialization Format={format}, Stores={string.Join(",", stores)}");
            }

            internal void BackupFailure(string details = null)
            {
                this.Log.LogError((int)EventIds.BackupFailure, $"Error occurred while attempting to create a database backup. Details: {details}.");
            }

            internal void DeletionError(string artifact, Exception ex)
            {
                this.Log.LogError((int)EventIds.DeletionError, $"An error occurred while deleting '{artifact}': {ex}");
            }

            internal void AllBackupsDeleted()
            {
                this.Log.LogInformation((int)EventIds.AllBackupsDeleted, "All existing backups have been deleted.");
            }

            internal void UnknownBackupsDeleted()
            {
                this.Log.LogInformation((int)EventIds.UnknownBackupsDeleted, "All unknown backups have been cleaned up.");
            }

            internal void BackupArtifactsCleanedUp()
            {
                this.Log.LogInformation((int)EventIds.BackupArtifactsCleanedUp, "Cleaned up the current backup's artifacts.");
            }

            internal void RestoringFromBackup(Guid backupId)
            {
                this.Log.LogInformation((int)EventIds.RestoringFromBackup, $"Starting restore of database from backup {backupId}.");
            }

            internal void NoBackupsForRestore()
            {
                this.Log.LogInformation((int)EventIds.NoBackupsForRestore, "No backups were found to restore database with.");
            }

            internal void RestoreComplete()
            {
                this.Log.LogInformation((int)EventIds.RestoreComplete, "Database restore from backup complete.");
            }

            internal void RestoreFailure(string details = null)
            {
                this.Log.LogError((int)EventIds.RestoreFailure, $"Error occurred while attempting a database restore from the last available backup. Details: {details}.");
            }
        }
    }
}
