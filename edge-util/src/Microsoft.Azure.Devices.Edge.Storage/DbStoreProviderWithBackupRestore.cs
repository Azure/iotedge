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
        readonly IDbStoreBackupRestore dbStoreBackupRestore;
        readonly SerializationFormat backupFormat;
        readonly Events events;

        DbStoreProviderWithBackupRestore(
            IDbStoreProvider dbStoreProvider,
            string backupPath,
            IDbStoreBackupRestore dbStoreBackupRestore,
            SerializationFormat backupFormat)
            : base(dbStoreProvider)
        {
            this.backupPath = Preconditions.CheckNonWhiteSpace(backupPath, nameof(backupPath));
            this.dbStoreBackupRestore = Preconditions.CheckNotNull(dbStoreBackupRestore, nameof(dbStoreBackupRestore));
            this.backupFormat = backupFormat;
            this.dbStores = new HashSet<string>();
            this.events = new Events(this.Log);
        }

        public static async Task<IDbStoreProvider> CreateAsync(
            IDbStoreProvider dbStoreProvider,
            string backupPath,
            IDbStoreBackupRestore dbStoreBackupRestore,
            SerializationFormat backupFormat)
        {
            DbStoreProviderWithBackupRestore provider = new DbStoreProviderWithBackupRestore(dbStoreProvider, backupPath, dbStoreBackupRestore, backupFormat);
            await provider.RestoreAsync();
            return provider;
        }

        private async Task RestoreAsync()
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

                string latestBackupDirPath = Path.Combine(backupPath, backupMetadata.Id.ToString());
                if (Directory.Exists(latestBackupDirPath))
                {
                    this.events.RestoringFromBackup(backupMetadata.Id);
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

                        await this.dbStoreBackupRestore.RestoreAsync(store, dbStore, latestBackupDirPath);
                    }

                    this.events.RestoreComplete();
                }
                else
                {
                    this.events.NoBackupsForRestore();
                }

                this.CleanupAllBackups(backupPath);
            }
            catch (IOException exception)
            {
                this.events.RestoreFailure($"The restore operation failed with error ${exception}.");

                // Clean up any restored state in the dictionary if the backup fails midway.
                foreach (string store in this.dbStores.ToList())
                {
                    this.RemoveDbStore(store);
                }

                this.RemoveDbStore();

                // Delete all backups as the last backup itself is corrupt.
                this.CleanupAllBackups(backupPath);
            }
        }

        public override IDbStore GetDbStore(string partitionName)
        {
            this.dbStores.Add(partitionName);
            return base.GetDbStore(partitionName);
        }

        public override IDbStore GetDbStore()
        {
            this.dbStores.Add(DefaultStoreBackupName);
            return base.GetDbStore();
        }

        public override void RemoveDbStore(string partitionName)
        {
            this.dbStores.Remove(partitionName);
            base.RemoveDbStore(partitionName);
        }

        public override void RemoveDbStore()
        {
            this.dbStores.Remove(DefaultStoreBackupName);
            base.RemoveDbStore();
        }

        public async override Task CloseAsync()
        {
            this.events.StartingBackup();
            Guid backupId = Guid.NewGuid();
            string dbBackupDirectory = Path.Combine(this.backupPath, backupId.ToString());

            BackupMetadata newBackupMetadata = new BackupMetadata(backupId, this.backupFormat, DateTime.UtcNow, this.dbStores.ToList());
            BackupMetadataList backupMetadataList = new BackupMetadataList(new List<BackupMetadata> { newBackupMetadata });
            try
            {
                Directory.CreateDirectory(dbBackupDirectory);

                // Backup other stores.
                foreach (string store in this.dbStores)
                {
                    IDbStore dbStore = this.dbStoreProvider.GetDbStore(store);
                    await this.dbStoreBackupRestore.BackupAsync(store, dbStore, dbBackupDirectory);
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
            catch (IOException exception)
            {
                this.events.BackupFailure($"The backup operation failed with error ${exception}.");

                // Clean up any artifacts of the attempted backup.
                this.CleanupKnownBackups(this.backupPath, backupMetadataList);
            }
        }

        private void CleanupAllBackups(string backupPath)
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

            this.events.AllBackupsDeleted();
        }

        private void CleanupUnknownBackups(string backupPath, BackupMetadataList metadataList)
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

            this.events.UnknownBackupsDeleted();
        }

        private void CleanupKnownBackups(string backupPath, BackupMetadataList metadataList)
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
            readonly ILogger Log;

            internal Events(ILogger logger)
            {
                this.Log = Preconditions.CheckNotNull(logger, nameof(logger));
            }

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

            internal void StartingBackup()
            {
                Log.LogInformation((int)EventIds.StartingBackup, "Starting backup of database.");
            }

            internal void BackupComplete()
            {
                this.Log.LogInformation((int)EventIds.BackupComplete, $"Backup of database complete.");
            }

            internal void BackupDirectoryNotFound(string backupDirectoryPath)
            {
                this.Log.LogInformation((int)EventIds.BackupDirectoryNotFound, $"The database backup directory {backupDirectoryPath} doesn't exist.");
            }

            internal void BackupInformation(Guid backupId, SerializationFormat format, DateTime backupTimestamp, IList<string> stores)
            {
                this.Log.LogDebug((int)EventIds.BackupInformation, $"Backup Info: Timestamp={backupTimestamp}, ID={backupId}, Serialization Format={format}, Stores={string.Join(",", stores)}");
            }

            internal void BackupFailure(string details = null)
            {
                this.Log.LogError((int)EventIds.BackupFailure, $"Error occurred while attempting to create a database backup. Details: {details}.");
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
