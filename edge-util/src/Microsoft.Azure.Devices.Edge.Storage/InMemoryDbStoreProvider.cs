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

    public class InMemoryDbStoreProvider : IDbStoreProvider
    {
        const string BackupMetadataFileName = "meta.json";
        const string DefaultPartitionName = "$Default";
        readonly ConcurrentDictionary<string, IDbStore> partitionDbStoreDictionary = new ConcurrentDictionary<string, IDbStore>();
        readonly Option<string> backupPath;
        readonly bool useBackupAndRestore;

        public InMemoryDbStoreProvider()
            : this(Option.None<string>(), false)
        {
        }

        public InMemoryDbStoreProvider(Option<string> backupPath, bool useBackupAndRestore)
        {
            this.backupPath = backupPath;
            this.useBackupAndRestore = useBackupAndRestore;

            // Restore from a previous backup if enabled.
            if (useBackupAndRestore)
            {
                string backupPathValue = this.backupPath.Expect(() => new ArgumentException($"The value of {nameof(backupPath)} needs to be specified if backup and restore is enabled."));
                Preconditions.CheckNonWhiteSpace(backupPathValue, nameof(backupPath));

                this.RestoreDb(backupPathValue);
            }
        }

        private void RestoreDb(string backupPath)
        {
            string backupMetadataFilePath = Path.Combine(backupPath, BackupMetadataFileName);

            if (!File.Exists(backupMetadataFilePath))
            {
                Events.NoBackupsForRestore();
                return;
            }

            try
            {
                using (StreamReader file = File.OpenText(backupMetadataFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    BackupMetadataList backupMetadataList = (BackupMetadataList)serializer.Deserialize(file, typeof(BackupMetadataList));
                    BackupMetadata backupMetadata = backupMetadataList.Backups[0];
                    Events.BackupInformation(backupMetadata.Id, backupMetadata.SerializationFormat, backupMetadata.TimestampUtc, backupMetadata.Stores);

                    string latestBackupDirPath = Path.Combine(backupPath, backupMetadata.Id.ToString());
                    if (Directory.Exists(latestBackupDirPath))
                    {
                        Events.RestoringFromBackup(backupMetadata.Id);
                        foreach (string store in backupMetadata.Stores)
                        {
                            InMemoryDbStore dbStore = new InMemoryDbStore(store, latestBackupDirPath);
                            this.partitionDbStoreDictionary.AddOrUpdate(store, dbStore, (_, __) => dbStore);
                        }

                        Events.RestoreComplete();
                    }
                    else
                    {
                        Events.NoBackupsForRestore();
                    }
                }

                CleanupAllBackups(backupPath);
            }
            catch (IOException exception)
            {
                Events.RestoreFailure($"The restore operation failed with error ${exception}.");

                // Clean up any restored state in the dictionary if the backup fails midway.
                this.partitionDbStoreDictionary.Clear();

                // Delete all backups as the last backup itself is corrupt.
                CleanupAllBackups(backupPath);
            }
        }

        public IDbStore GetDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            IDbStore dbStore = this.partitionDbStoreDictionary.GetOrAdd(partitionName, new InMemoryDbStore(partitionName));
            return dbStore;
        }

        public IDbStore GetDbStore() => this.GetDbStore(DefaultPartitionName);

        public void RemoveDbStore(string partitionName)
        {
            Preconditions.CheckNonWhiteSpace(partitionName, nameof(partitionName));
            this.partitionDbStoreDictionary.TryRemove(partitionName, out IDbStore _);
        }

        public void Close()
        {
            this.CloseAsync().Wait();
        }

        public async Task CloseAsync()
        {
            if (this.useBackupAndRestore)
            {
                Events.StartingBackup();
                string backupPathValue = this.backupPath.Expect(() => new InvalidOperationException($"The value of {nameof(this.backupPath)} is expected to be a valid path if backup and restore is enabled."));
                Guid backupId = Guid.NewGuid();
                string dbBackupDirectory = Path.Combine(backupPathValue, backupId.ToString());

                BackupMetadata newBackupMetadata = new BackupMetadata(backupId, SerializationFormat.ProtoBuf, DateTime.UtcNow, this.partitionDbStoreDictionary.Keys.ToList());
                BackupMetadataList backupMetadataList = new BackupMetadataList(new List<BackupMetadata> { newBackupMetadata });
                try
                {
                    Directory.CreateDirectory(dbBackupDirectory);
                    foreach (IDbStore dbStore in this.partitionDbStoreDictionary.Values)
                    {
                        await dbStore.BackupAsync(dbBackupDirectory);
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
        }

        public void Dispose()
        {
            // No-op
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
