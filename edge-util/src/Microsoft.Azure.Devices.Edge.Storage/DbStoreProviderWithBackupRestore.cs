// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class DbStoreProviderWithBackupRestore : DbStoreProviderDecorator
    {
        const string BackupMetadataFileName = "meta.json";
        const string DefaultStoreBackupName = "$Default";
        readonly string backupPath;
        readonly ISet<string> dbStores;

        DbStoreProviderWithBackupRestore(string backupPath, IDbStoreProvider dbStoreProvider)
            : base(dbStoreProvider)
        {
            Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));
            this.backupPath = backupPath;
        }

        public static async Task<DbStoreProviderWithBackupRestore> CreateAsync(string backupPath, IDbStoreProvider dbStoreProvider)
        {
            DbStoreProviderWithBackupRestore provider = new DbStoreProviderWithBackupRestore(backupPath, dbStoreProvider);
            await provider.RestoreAsync();
            return provider;
        }

        private async Task RestoreAsync()
        {
            string backupMetadataFilePath = Path.Combine(this.backupPath, BackupMetadataFileName);

            if (!File.Exists(backupMetadataFilePath))
            {
                Events.NoBackupsForRestore();
                return;
            }

            try
            {
                string fileText = File.ReadAllText(backupMetadataFilePath);
                BackupMetadataList backupMetadataList = JsonConvert.DeserializeObject<BackupMetadataList>(fileText);
                BackupMetadata backupMetadata = backupMetadataList.Backups[0];
                Events.BackupInformation(backupMetadata.Id, backupMetadata.SerializationFormat, backupMetadata.TimestampUtc, backupMetadata.Stores);

                string latestBackupDirPath = Path.Combine(backupPath, backupMetadata.Id.ToString());
                if (Directory.Exists(latestBackupDirPath))
                {
                    Events.RestoringFromBackup(backupMetadata.Id);
                    foreach (string store in backupMetadata.Stores)
                    {
                        IDbStore dbStore;
                        if (!store.Equals(DefaultStoreBackupName, StringComparison.OrdinalIgnoreCase)) {
                            dbStore = this.GetDbStore(store);
                        }
                        else
                        {
                            dbStore = this.GetDbStore();
                        }

                        await this.RestoreDbStoreAsync(store, dbStore, latestBackupDirPath);
                    }

                    Events.RestoreComplete();
                }
                else
                {
                    Events.NoBackupsForRestore();
                }

                CleanupAllBackups(backupPath);
            }
            catch (IOException exception)
            {
                Events.RestoreFailure($"The restore operation failed with error ${exception}.");

                // Clean up any restored state in the dictionary if the backup fails midway.
                foreach (string store in this.dbStores)
                {
                    this.RemoveDbStore(store);
                }

                this.RemoveDbStore();

                // Delete all backups as the last backup itself is corrupt.
                CleanupAllBackups(backupPath);
            }
        }

        public override IDbStore GetDbStore(string partitionName)
        {
            this.dbStores.Add(partitionName);
            return base.GetDbStore(partitionName);
        }

        public override IDbStore GetDbStore()
        {
            return base.GetDbStore();
        }

        public override void RemoveDbStore(string partitionName)
        {
            this.dbStores.Remove(partitionName);
            base.RemoveDbStore(partitionName);
        }

        public async override Task CloseAsync()
        {
            Events.StartingBackup();
            string backupPathValue = this.backupPath;
            Guid backupId = Guid.NewGuid();
            string dbBackupDirectory = Path.Combine(backupPathValue, backupId.ToString());

            BackupMetadata newBackupMetadata = new BackupMetadata(backupId, SerializationFormat.ProtoBuf, DateTime.UtcNow, this.dbStores.ToList());
            BackupMetadataList backupMetadataList = new BackupMetadataList(new List<BackupMetadata> { newBackupMetadata });
            try
            {
                Directory.CreateDirectory(dbBackupDirectory);

                // Backup default store.
                IDbStore dbStore = this.dbStoreProvider.GetDbStore();
                await this.BackupDbStoreAsync(DefaultStoreBackupName, dbStore, dbBackupDirectory);

                // Backup other stores.
                foreach (string store in this.dbStores)
                {
                    dbStore = this.dbStoreProvider.GetDbStore(store);
                    await this.BackupDbStoreAsync(store, dbStore, dbBackupDirectory);
                }

                using (StreamWriter file = File.CreateText(Path.Combine(backupPathValue, BackupMetadataFileName)))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, backupMetadataList);
                }

                Events.BackupComplete();

                // Clean any old backups.
                CleanupUnknownBackups(backupPathValue, backupMetadataList);
            }
            catch (IOException exception)
            {
                Events.BackupFailure($"The backup operation failed with error ${exception}.");

                // Clean up any artifacts of the attempted backup.
                CleanupKnownBackups(backupPathValue, backupMetadataList);
            }
        }

        private static void CleanupAllBackups(string backupPath)
        {
            DirectoryInfo backupDirInfo = new DirectoryInfo(backupPath);
            foreach (FileInfo file in backupDirInfo.GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo dir in backupDirInfo.GetDirectories())
            {
                dir.Delete(true);
            }

            Events.AllBackupsDeleted();
        }

        private static void CleanupUnknownBackups(string backupPath, BackupMetadataList metadataList)
        {
            DirectoryInfo backupDirInfo = new DirectoryInfo(backupPath);
            HashSet<string> knownBackupDirNames = new HashSet<string>(metadataList.Backups.Select(x => x.Id.ToString()), StringComparer.OrdinalIgnoreCase);
            foreach (DirectoryInfo dir in backupDirInfo.GetDirectories())
            {
                if (!knownBackupDirNames.Contains(dir.Name))
                {
                    dir.Delete(true);
                }
            }

            Events.UnknownBackupsDeleted();
        }

        private static void CleanupKnownBackups(string backupPath, BackupMetadataList metadataList)
        {
            DirectoryInfo backupDirInfo = new DirectoryInfo(backupPath);
            HashSet<string> knownBackupDirNames = new HashSet<string>(metadataList.Backups.Select(x => x.Id.ToString()), StringComparer.OrdinalIgnoreCase);
            foreach (DirectoryInfo dir in backupDirInfo.GetDirectories())
            {
                if (knownBackupDirNames.Contains(dir.Name))
                {
                    dir.Delete(true);
                }
            }

            Events.BackupArtifactsCleanedUp();
        }

        private async Task RestoreDbStoreAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(backupPath);
            try
            {
                ItemKeyedCollection items = await itemKeyedCollectionBackupRestore.RestoreAsync(entityName);
                foreach (Item item in items)
                {
                    await dbStore.Put(item.Key, item.Value);
                }
            }
            catch (IOException exception)
            {
                throw new IOException($"The restore operation for {entityName} failed with error.", exception);
            }
        }

        private async Task BackupDbStoreAsync(string entityName, IDbStore dbStore, string backupPath)
        {
            IItemKeyedCollectionBackupRestore itemKeyedCollectionBackupRestore = new ItemKeyedCollectionBackupRestore(backupPath);
            try
            {
                // This is a hack, make it better by not having to create another in-memory collection of items
                // to be backed up.
                ItemKeyedCollection items = new ItemKeyedCollection(new ByteArrayComparer());
                await dbStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    items.Add(new Item(key, value));
                    return Task.CompletedTask;
                });

                await itemKeyedCollectionBackupRestore.BackupAsync(entityName, items);
            }
            catch (IOException exception)
            {
                throw new IOException($"The backup operation for {entityName} failed with error.", exception);
            }
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

        enum SerializationFormat
        {
            ProtoBuf = 0,
        }

        static class Events
        {
            const int IdStart = UtilEventsIds.InMemoryDbStoreProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<InMemoryDbStoreProvider>();

            enum EventIds
            {
                StartingBackup = IdStart,
                AllBackupsDeleted,
                UnknownBackupsDeleted,
                BackupArtifactsCleanedUp,
                BackupComplete,
                BackupDirectoryNotFound,
                BackupInformation,
                BackupFailure,
                RestoringFromBackup,
                NoBackupsForRestore,
                RestoreComplete,
                RestoreFailure
            }

            internal static void StartingBackup()
            {
                Log.LogInformation((int)EventIds.StartingBackup, "Starting backup of database.");
            }

            internal static void BackupComplete()
            {
                Log.LogInformation((int)EventIds.BackupComplete, $"Backup of database complete.");
            }

            internal static void BackupDirectoryNotFound(string backupDirectoryPath)
            {
                Log.LogInformation((int)EventIds.BackupDirectoryNotFound, $"The database backup directory {backupDirectoryPath} doesn't exist.");
            }

            internal static void BackupInformation(Guid backupId, SerializationFormat format, DateTime backupTimestamp, IList<string> stores)
            {
                Log.LogDebug((int)EventIds.BackupInformation, $"Backup Info: Timestamp={backupTimestamp}, ID={backupId}, Serialization Format={format}, Stores={string.Join(",", stores)}");
            }

            internal static void BackupFailure(string details = null)
            {
                Log.LogError((int)EventIds.BackupFailure, $"Error occurred while attempting to create a database backup. Details: {details}.");
            }

            internal static void AllBackupsDeleted()
            {
                Log.LogInformation((int)EventIds.AllBackupsDeleted, "All existing backups have been deleted.");
            }

            internal static void UnknownBackupsDeleted()
            {
                Log.LogInformation((int)EventIds.UnknownBackupsDeleted, "All unknown backups have been cleaned up.");
            }

            internal static void BackupArtifactsCleanedUp()
            {
                Log.LogInformation((int)EventIds.BackupArtifactsCleanedUp, "Cleaned up the current backup's artifacts.");
            }

            internal static void RestoringFromBackup(Guid backupId)
            {
                Log.LogInformation((int)EventIds.RestoringFromBackup, $"Starting restore of database from backup {backupId}.");
            }

            internal static void NoBackupsForRestore()
            {
                Log.LogInformation((int)EventIds.NoBackupsForRestore, "No backups were found to restore database with.");
            }

            internal static void RestoreComplete()
            {
                Log.LogInformation((int)EventIds.RestoreComplete, "Database restore from backup complete.");
            }

            internal static void RestoreFailure(string details = null)
            {
                Log.LogError((int)EventIds.RestoreFailure, $"Error occurred while attempting a database restore from the last available backup. Details: {details}.");
            }
        }
    }
}
