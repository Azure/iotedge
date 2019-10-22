// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DbStoreProviderWithBackupRestoreTest
    {
        readonly string backupFolder;

        public DbStoreProviderWithBackupRestoreTest()
        {
            string tempFolder = Path.GetTempPath();
            this.backupFolder = Path.Combine(tempFolder, $"dbStoreProviderTestBackup{Guid.NewGuid()}");
            if (Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder);
            }

            Directory.CreateDirectory(this.backupFolder);
        }

        ~DbStoreProviderWithBackupRestoreTest()
        {
            if (!string.IsNullOrWhiteSpace(this.backupFolder) && Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder, true);
            }
        }

        [Fact]
        public async Task CreateInvalidInputTestAsync()
        {
            // Invalid DbStoreProvider.
            IDataBackupRestore dataBackupRestore = Mock.Of<IDataBackupRestore>();
            await Assert.ThrowsAsync<ArgumentNullException>(() => DbStoreProviderWithBackupRestore.CreateAsync(null, this.backupFolder, dataBackupRestore));

            // Invalid backup path.
            IDbStoreProvider dbStoreProvider = Mock.Of<IDbStoreProvider>();
            await Assert.ThrowsAsync<ArgumentException>(() => DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider, null, dataBackupRestore));
            await Assert.ThrowsAsync<ArgumentException>(() => DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider, " ", dataBackupRestore));

            // Invalid dataBackupRestore.
            await Assert.ThrowsAsync<ArgumentNullException>(() => DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider, this.backupFolder, null));
        }

        [Fact]
        public async Task BackupRestoreSuccessTest()
        {
            var dataBackupRestore = new Mock<IDataBackupRestore>();
            var dbStoreProvider = new Mock<IDbStoreProvider>();

            IDbStoreProvider dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            dbStoreProvider.Setup(x => x.GetDbStore()).Returns(Mock.Of<IDbStore>());
            dbStoreProvider.Setup(x => x.GetDbStore(It.IsAny<string>())).Returns(Mock.Of<IDbStore>());

            // Get default DB store.
            dbStoreProviderWithBackupRestore.GetDbStore();

            // Create custom DB stores.
            string[] storeNames = new[] { "store1", "store2", "store3" };
            storeNames.Select(x => dbStoreProviderWithBackupRestore.GetDbStore(x)).ToList();

            // Remove one of the created stores.
            dbStoreProviderWithBackupRestore.RemoveDbStore(storeNames[0]);

            // Since we added 3 stores and then removed 1, the total number of stores now will be 3 (2 custom + 1 default).

            // Create mock backup directories in the backup folder to test if the backup operation cleans up older backups or not.
            IList<DirectoryInfo> mockBackupDirectories = new List<DirectoryInfo>(2);
            for (int i = 0; i < 2; i++)
            {
                mockBackupDirectories.Add(Directory.CreateDirectory(Path.Combine(this.backupFolder, Guid.NewGuid().ToString())));
            }

            // Close the DB store provider now. This should execute the backup operations.
            await dbStoreProviderWithBackupRestore.CloseAsync();

            // Assert that all the remaining stores created above were backed up.
            dataBackupRestore.Verify(m => m.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<Item>>()), Times.Exactly(3));

            ValidateBackupArtifacts(this.backupFolder);

            // Validate that other backups have been deleted.
            foreach (DirectoryInfo mockBackupDir in mockBackupDirectories)
            {
                mockBackupDir.Refresh();
                Assert.False(mockBackupDir.Exists);
            }

            // Test restore.
            dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            dataBackupRestore.Verify(m => m.RestoreAsync<IList<Item>>(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));

            // All backups should be deleted after a successful restore.
            IsDirectoryEmpty(this.backupFolder);
        }

        [Fact]
        public async Task BackupFailureCleanupTest()
        {
            var dataBackupRestore = new Mock<IDataBackupRestore>();
            var dbStoreProvider = new Mock<IDbStoreProvider>();

            dataBackupRestore.Setup(x => x.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<Item>>())).Throws(new IOException());
            IDbStoreProvider dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            dbStoreProvider.Setup(x => x.GetDbStore()).Returns(Mock.Of<IDbStore>());

            // Get the default DB store.
            dbStoreProviderWithBackupRestore.GetDbStore();

            // Create mock backup directories in the backup folder to test if the backup operation cleans up older backups or not.
            IList<DirectoryInfo> mockBackupDirectories = new List<DirectoryInfo>(2);
            for (int i = 0; i < 2; i++)
            {
                mockBackupDirectories.Add(Directory.CreateDirectory(Path.Combine(this.backupFolder, Guid.NewGuid().ToString())));
            }

            // Close the DB store provider now. This should execute the backup operations which will fail.
            await dbStoreProviderWithBackupRestore.CloseAsync();

            int numberOfDirs = 0;

            // Validate that other artifacts have not been deleted.
            foreach (DirectoryInfo mockBackupDir in mockBackupDirectories)
            {
                mockBackupDir.Refresh();
                Assert.True(mockBackupDir.Exists);
                numberOfDirs++;
            }

            // No new backups artifacts should be present.
            Assert.Equal(mockBackupDirectories.Count, numberOfDirs);
        }

        [Fact]
        public async Task RestoreFailureTest()
        {
            var dataBackupRestore = new Mock<IDataBackupRestore>();
            var dbStoreProvider = new Mock<IDbStoreProvider>();

            IDbStoreProvider dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            dbStoreProvider.Setup(x => x.GetDbStore()).Returns(Mock.Of<IDbStore>());
            dbStoreProvider.Setup(x => x.GetDbStore(It.IsAny<string>())).Returns(Mock.Of<IDbStore>());

            // Get default DB store.
            dbStoreProviderWithBackupRestore.GetDbStore();

            // Create custom DB stores.
            string[] storeNames = new[] { "store1", "store2", "store3" };
            storeNames.Select(x => dbStoreProviderWithBackupRestore.GetDbStore(x)).ToList();

            // Close the DB store provider now. This should execute the backup operations.
            await dbStoreProviderWithBackupRestore.CloseAsync();

            // Assert that all the remaining stores created above were backed up.
            dataBackupRestore.Verify(m => m.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<Item>>()), Times.Exactly(storeNames.Length + 1));

            ValidateBackupArtifacts(this.backupFolder);

            // Create mock directories in the backup folder to test if the restore failure operation cleans up all artifacts or not.
            IList<DirectoryInfo> mockBackupDirectories = new List<DirectoryInfo>(2);
            for (int i = 0; i < 2; i++)
            {
                mockBackupDirectories.Add(Directory.CreateDirectory(Path.Combine(this.backupFolder, Guid.NewGuid().ToString())));
            }

            // Throw exception when a restore attempt is made.
            dataBackupRestore.Setup(m => m.RestoreAsync<IList<Item>>(It.IsAny<string>(), It.IsAny<string>())).Throws(new IOException());

            // Test restore failure.
            dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            // Attempts to remove the DB stores should have been made as part of fallback from the restore failure.
            dbStoreProvider.Verify(x => x.RemoveDbStore(), Times.Once);
            dbStoreProvider.Verify(x => x.RemoveDbStore(It.IsAny<string>()), Times.Once);

            // All backups should be deleted after a successful restore.
            IsDirectoryEmpty(this.backupFolder);
        }

        [Fact]
        public async Task RestoreCorruptMetadataFailureTest()
        {
            var dataBackupRestore = new Mock<IDataBackupRestore>();
            var dbStoreProvider = new Mock<IDbStoreProvider>();

            IDbStoreProvider dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            dbStoreProvider.Setup(x => x.GetDbStore()).Returns(Mock.Of<IDbStore>());
            dbStoreProvider.Setup(x => x.GetDbStore(It.IsAny<string>())).Returns(Mock.Of<IDbStore>());

            // Get default DB store.
            dbStoreProviderWithBackupRestore.GetDbStore();

            // Create custom DB stores.
            string[] storeNames = new[] { "store1", "store2", "store3" };
            storeNames.Select(x => dbStoreProviderWithBackupRestore.GetDbStore(x)).ToList();

            // Close the DB store provider now. This should execute the backup operations.
            await dbStoreProviderWithBackupRestore.CloseAsync();

            // Assert that all the remaining stores created above were backed up.
            dataBackupRestore.Verify(m => m.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<Item>>()), Times.Exactly(storeNames.Length + 1));

            ValidateBackupArtifacts(this.backupFolder);

            // Create mock directories in the backup folder to test if the restore failure operation cleans up all artifacts or not.
            IList<DirectoryInfo> mockBackupDirectories = new List<DirectoryInfo>(2);
            for (int i = 0; i < 2; i++)
            {
                mockBackupDirectories.Add(Directory.CreateDirectory(Path.Combine(this.backupFolder, Guid.NewGuid().ToString())));
            }

            // Corrupt the backup metadata.
            using (FileStream file = File.OpenWrite(Path.Combine(this.backupFolder, "meta.json")))
            {
                file.Write(new byte[] { 1, 2 }, 1, 1);
            }

            // Test restore failure.
            dbStoreProviderWithBackupRestore =
                await DbStoreProviderWithBackupRestore.CreateAsync(dbStoreProvider.Object, this.backupFolder, dataBackupRestore.Object);

            // No attempts to restore a DB should have been made as the metadata itself was bad.
            dataBackupRestore.Verify(m => m.RestoreAsync<IList<Item>>(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // Attempts to remove only the default DB stores should have been made.
            dbStoreProvider.Verify(x => x.RemoveDbStore(), Times.Once);
            dbStoreProvider.Verify(x => x.RemoveDbStore(It.IsAny<string>()), Times.Never);

            // All backups should be deleted after a successful restore.
            IsDirectoryEmpty(this.backupFolder);
        }

        static bool IsDirectoryEmpty(string path)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            return dirInfo.GetDirectories().Count() == 0 && dirInfo.GetFiles().Count() == 0;
        }

        static void ValidateBackupArtifacts(string path)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            Assert.Single(dirInfo.GetDirectories());
            Assert.Single(dirInfo.GetFiles());
        }
    }
}
