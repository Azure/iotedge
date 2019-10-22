// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using ProtoBuf;
    using Xunit;

    [Unit]
    public class ProtoBufDataBackupRestoreTest
    {
        readonly string backupFolder;

        public ProtoBufDataBackupRestoreTest()
        {
            string tempFolder = Path.GetTempPath();
            this.backupFolder = Path.Combine(tempFolder, $"protoBufTestBackup{Guid.NewGuid()}");
            if (Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder);
            }

            Directory.CreateDirectory(this.backupFolder);
        }

        ~ProtoBufDataBackupRestoreTest()
        {
            if (!string.IsNullOrWhiteSpace(this.backupFolder) && Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder, true);
            }
        }

        [Fact]
        public async Task BackupInvalidInputTestAsync()
        {
            ProtoBufDataBackupRestore backupRestore = new ProtoBufDataBackupRestore();
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync(null, "abc", "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync(" ", "abc", "test"));

            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync("abc", null, "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync("abc", " ", "test"));

            await Assert.ThrowsAsync<IOException>(() => backupRestore.BackupAsync("abc", "abc", "C:\\" + Guid.NewGuid().ToString()));
        }

        [Fact]
        public async Task RestoreInvalidInputTestAsync()
        {
            ProtoBufDataBackupRestore backupRestore = new ProtoBufDataBackupRestore();
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>(null, "abc"));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>(" ", "abc"));

            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>("abc", null));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>("abc", " "));

            await Assert.ThrowsAsync<IOException>(() => backupRestore.BackupAsync("abc", "abc", "C:\\" + Guid.NewGuid().ToString()));
        }

        [Fact]
        public async Task RestoreInvalidBackupDataTestAsync()
        {
            ProtoBufDataBackupRestore backupRestore = new ProtoBufDataBackupRestore();

            Item item1 = new Item("key1".ToBytes(), "val1".ToBytes());
            Item item2 = new Item("key2".ToBytes(), "val2".ToBytes());
            IList<Item> items = new List<Item> { item1, item2 };

            string name = "test";
            await backupRestore.BackupAsync(name, this.backupFolder, items);

            // Corrupt the backup file.
            using (FileStream file = File.OpenWrite(Path.Combine(this.backupFolder, $"{name}.bin")))
            {
                file.Write(new byte[] { 1, 2 }, 1, 1);
            }

            // Attempting a restore should now fail.
            Exception ex = await Assert.ThrowsAsync<IOException>(() => backupRestore.RestoreAsync<IList<Item>>(name, this.backupFolder));
            Assert.True(ex.InnerException is ProtoException);
        }

        [Fact]
        public async Task BackupRestoreSuccessTest()
        {
            ProtoBufDataBackupRestore backupRestore = new ProtoBufDataBackupRestore();

            Item item1 = new Item("key1".ToBytes(), "val1".ToBytes());
            Item item2 = new Item("key2".ToBytes(), "val2".ToBytes());
            IList<Item> items = new List<Item> { item1, item2 };

            string name = "test";
            await backupRestore.BackupAsync(name, this.backupFolder, items);

            // Attempt a restore to validate that that backup succeeds.
            IList<Item> restoredItems = await backupRestore.RestoreAsync<IList<Item>>(name, this.backupFolder);
            Assert.Equal(items.Count, restoredItems.Count);
            Assert.All(items, x => restoredItems.Any(y => x.Key.SequenceEqual(y.Key) && x.Value.SequenceEqual(y.Value)));
        }
    }
}
