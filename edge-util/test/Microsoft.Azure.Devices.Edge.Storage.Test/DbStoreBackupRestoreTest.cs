// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DbStoreBackupRestoreTest
    {
        readonly string backupFolder;

        public DbStoreBackupRestoreTest()
        {
            string tempFolder = Path.GetTempPath();
            this.backupFolder = Path.Combine(tempFolder, $"dbStoreTestBackup{Guid.NewGuid()}");
            if (Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder);
            }

            Directory.CreateDirectory(this.backupFolder);
        }

        ~DbStoreBackupRestoreTest()
        {
            if (!string.IsNullOrWhiteSpace(this.backupFolder) && Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder, true);
            }
        }

        [Fact]
        public async Task BackupInvalidInputTestAsync()
        {
            var backupRestore = new Mock<IBackupRestore>();
            DbStoreBackupRestore dbStorebackupRestore = new DbStoreBackupRestore(backupRestore.Object);

            var dbStore = Mock.Of<IDbStore>(c => c.IterateBatch(It.IsAny<int>(), It.IsAny<Func<byte[], byte[], Task>>()) == Task.CompletedTask);
            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.BackupAsync(null, dbStore, "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.BackupAsync(" ", dbStore, "test"));

            await Assert.ThrowsAsync<ArgumentNullException>(() => dbStorebackupRestore.BackupAsync("abc", null, "abc"));

            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.BackupAsync("abc", dbStore, null));
            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.BackupAsync("abc", dbStore, " "));

            backupRestore.Setup(c => c.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<Item>>())).Throws(new IOException());
            dbStorebackupRestore = new DbStoreBackupRestore(backupRestore.Object);

            IOException ex = await Assert.ThrowsAsync<IOException>(() => dbStorebackupRestore.BackupAsync("abc", dbStore, "abc"));
            Assert.True(ex.InnerException is IOException);
        }

        [Fact]
        public async Task RestoreInvalidInputTestAsync()
        {
            var backupRestore = new Mock<IBackupRestore>();
            DbStoreBackupRestore dbStorebackupRestore = new DbStoreBackupRestore(backupRestore.Object);

            var dbStore = Mock.Of<IDbStore>(c => c.Put(It.IsAny<byte[]>(), It.IsAny<byte[]>()) == Task.CompletedTask);
            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.RestoreAsync(null, dbStore, "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.RestoreAsync(" ", dbStore, "test"));

            await Assert.ThrowsAsync<ArgumentNullException>(() => dbStorebackupRestore.RestoreAsync("abc", null, "abc"));

            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.RestoreAsync("abc", dbStore, null));
            await Assert.ThrowsAsync<ArgumentException>(() => dbStorebackupRestore.RestoreAsync("abc", dbStore, " "));

            backupRestore.Setup(c => c.RestoreAsync<IList<Item>>(It.IsAny<string>(), It.IsAny<string>())).Throws(new IOException());
            dbStorebackupRestore = new DbStoreBackupRestore(backupRestore.Object);

            IOException ex = await Assert.ThrowsAsync<IOException>(() => dbStorebackupRestore.RestoreAsync("abc", dbStore, "abc"));
            Assert.True(ex.InnerException is IOException);
        }

        [Fact]
        public async Task BackupSuccessTestAsync()
        {
            var backupRestore = new Mock<IBackupRestore>();
            DbStoreBackupRestore dbStorebackupRestore = new DbStoreBackupRestore(backupRestore.Object);

            IDbStore dbStore = new InMemoryDbStore();
            await dbStorebackupRestore.BackupAsync("abc", dbStore, "abc");

            backupRestore.Verify(m => m.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<Item>>()), Times.Once);
        }

        [Fact]
        public async Task RestoreSuccessTestAsync()
        {
            var backupRestore = new Mock<IBackupRestore>();
            DbStoreBackupRestore dbStorebackupRestore = new DbStoreBackupRestore(backupRestore.Object);

            IDbStore dbStore = new InMemoryDbStore();
            await dbStorebackupRestore.RestoreAsync("abc", dbStore, "abc");

            backupRestore.Verify(m => m.RestoreAsync<IList<Item>>(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }
}
