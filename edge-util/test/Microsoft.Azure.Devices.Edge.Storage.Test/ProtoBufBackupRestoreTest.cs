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
    public class ProtoBufBackupRestoreTest
    {
        readonly string backupFolder;

        public ProtoBufBackupRestoreTest()
        {
            string tempFolder = Path.GetTempPath();
            this.backupFolder = Path.Combine(tempFolder, $"protoBufTestBackup{Guid.NewGuid()}");
            if (Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder);
            }

            Directory.CreateDirectory(this.backupFolder);
        }

        ~ProtoBufBackupRestoreTest()
        {
            if (!string.IsNullOrWhiteSpace(this.backupFolder) && Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder, true);
            }
        }

        [Fact]
        public async Task BackupInvalidInputTestAsync()
        {
            ProtoBufBackupRestore backupRestore = new ProtoBufBackupRestore();
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync(null, "abc", "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync(" ", "abc", "test"));

            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync("abc", null, "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.BackupAsync("abc", " ", "test"));

            await Assert.ThrowsAsync<IOException>(() => backupRestore.BackupAsync("abc", "abc", "C:\\" + Guid.NewGuid().ToString()));
        }

        [Fact]
        public async Task RestoreInvalidInputTestAsync()
        {
            ProtoBufBackupRestore backupRestore = new ProtoBufBackupRestore();
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>(null, "abc"));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>(" ", "abc"));

            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>("abc", null));
            await Assert.ThrowsAsync<ArgumentException>(() => backupRestore.RestoreAsync<string>("abc", " "));

            await Assert.ThrowsAsync<IOException>(() => backupRestore.BackupAsync("abc", "abc", "C:\\" + Guid.NewGuid().ToString()));
        }

        [Fact]
        public async Task RestoreInvalidBackupDataTestAsync()
        {
            ProtoBufBackupRestore backupRestore = new ProtoBufBackupRestore();

            Item item1 = new Item("key1".ToBytes(), "val1".ToBytes());
            Item item2 = new Item("key2".ToBytes(), "val2".ToBytes());
            IList<Item> items = new List<Item> { item1, item2 };

            string name = "test";
            await backupRestore.BackupAsync(name, backupFolder, items);

            // Corrupt the backup file.
            using (FileStream file = File.OpenWrite(Path.Combine(backupFolder, $"{name}.bin")))
            {
                file.Write(new byte[] { 1, 2 }, 1, 1);
            }

            // Attempting a restore should now fail.
            Exception ex = await Assert.ThrowsAsync<IOException>(() => backupRestore.RestoreAsync<IList<Item>>(name, backupFolder));
            Assert.True(ex.InnerException is ProtoException);
        }

        [Fact]
        public async Task BackupRestoreSuccessTest()
        {
            ProtoBufBackupRestore backupRestore = new ProtoBufBackupRestore();

            Item item1 = new Item("key1".ToBytes(), "val1".ToBytes());
            Item item2 = new Item("key2".ToBytes(), "val2".ToBytes());
            IList<Item> items = new List<Item> { item1, item2 };

            string name = "test";
            await backupRestore.BackupAsync(name, backupFolder, items);

            // Attempt a restore to validate that that backup succeeds.
            IList<Item> restoredItems = await backupRestore.RestoreAsync<IList<Item>>(name, backupFolder);
            Assert.Equal(items.Count, restoredItems.Count);
            Assert.All(items, x => restoredItems.Any(y => x.Key.SequenceEqual(y.Key) && x.Value.SequenceEqual(y.Value)));
        }

        //[Fact]
        //public async Task BackupSuccessTest()
        //{
        //    var provider = new InMemoryDbStoreProvider(Option.Some(this.backupFolder), true);

        //    string[] storeNames = new[] { "store1", "store2" };
        //    IEnumerable<IDbStore> stores = storeNames.Select(x => provider.GetDbStore(x));

        //    foreach (IDbStore store in stores)
        //    {
        //        await store.Put("key1".ToBytes(), "val1".ToBytes());
        //        await store.Put("key2".ToBytes(), "val2".ToBytes());
        //        await store.Put("key3".ToBytes(), "val3".ToBytes());
        //    }

        //    Func<IDbStore, Task> dbValidator = async (IDbStore dbStore) =>
        //    {
        //        Option<(byte[] key, byte[] value)> firstValueOption = await dbStore.GetFirstEntry();
        //        Assert.True(firstValueOption.HasValue);
        //        (byte[] key, byte[] value) firstValue = firstValueOption.OrDefault();
        //        Assert.Equal("key1", firstValue.key.FromBytes<string>());
        //        Assert.Equal("val1", firstValue.value.FromBytes<string>());

        //        Option<(byte[] key, byte[] value)> lastValueOption = await dbStore.GetLastEntry();
        //        Assert.True(lastValueOption.HasValue);
        //        (byte[] key, byte[] value) lastValue = lastValueOption.OrDefault();
        //        Assert.Equal("key3", lastValue.key.FromBytes<string>());
        //        Assert.Equal("val3", lastValue.value.FromBytes<string>());
        //    };

        //    foreach (IDbStore store in stores)
        //    {
        //        await dbValidator(store);
        //    }

        //    // Create mock backup directories in the backup folder to test if the backup operation cleans up older backups or not.
        //    IList<DirectoryInfo> mockBackupDirectories = new List<DirectoryInfo>(2);
        //    for (int i = 0; i < 2; i++)
        //    {
        //        mockBackupDirectories.Add(Directory.CreateDirectory(Path.Combine(this.backupFolder, Guid.NewGuid().ToString())));
        //    }

        //    // Force the creation of a backup.
        //    provider.Close();

        //    ValidateBackupArtifacts(this.backupFolder);

        //    // Validate that other backups have been deleted.
        //    foreach (DirectoryInfo mockBackupDir in mockBackupDirectories)
        //    {
        //        mockBackupDir.Refresh();
        //        Assert.False(mockBackupDir.Exists);
        //    }

        //    // Create a new DB store provider and restore from the created backup.
        //    var provider2 = new InMemoryDbStoreProvider(Option.Some(this.backupFolder), true);
        //    stores = storeNames.Select(x => provider2.GetDbStore(x));

        //    foreach (IDbStore store in stores)
        //    {
        //        await dbValidator(store);
        //    }

        //    Assert.True(IsDirectoryEmpty(this.backupFolder));
        //}

        //[Fact]
        //public async Task RestoreFailureTest()
        //{
        //    var provider = new InMemoryDbStoreProvider(Option.Some(this.backupFolder), true);

        //    string[] storeNames = new[] { "store1", "store2" };
        //    IEnumerable<IDbStore> stores = storeNames.Select(x => provider.GetDbStore(x));

        //    foreach (IDbStore store in stores)
        //    {
        //        await store.Put("key1".ToBytes(), "val1".ToBytes());
        //        await store.Put("key2".ToBytes(), "val2".ToBytes());
        //        await store.Put("key3".ToBytes(), "val3".ToBytes());
        //    }

        //    // Force the creation of a backup.
        //    provider.Close();
        //    ValidateBackupArtifacts(this.backupFolder);

        //    // Corrupt the backup data.
        //    DirectoryInfo backupFolderInfo = new DirectoryInfo(this.backupFolder);
        //    DirectoryInfo newBackupDirInfo = backupFolderInfo.GetDirectories()[0];
        //    FileInfo backupDataFile1 = newBackupDirInfo.GetFiles()[0];
        //    backupDataFile1.Delete();

        //    // Create a new DB store provider and restore from the created backup. This will fail as the backup is corrupt.
        //    var provider2 = new InMemoryDbStoreProvider(Option.Some(this.backupFolder), true);

        //    // Validate that data added earlier to the backup is lost.
        //    var newStore = provider2.GetDbStore(storeNames[0]);
        //    Option<(byte[] key, byte[] value)> value = await newStore.GetFirstEntry();
        //    Assert.False(value.HasValue);

        //    // The older backups should be cleaned up as well since they are corrupt.
        //    Assert.True(IsDirectoryEmpty(this.backupFolder));
        //}

        //static bool IsDirectoryEmpty(string path)
        //{
        //    DirectoryInfo dirInfo = new DirectoryInfo(path);
        //    return dirInfo.GetDirectories().Count() == 0 && dirInfo.GetFiles().Count() == 0;
        //}

        //static void ValidateBackupArtifacts(string path)
        //{
        //    DirectoryInfo dirInfo = new DirectoryInfo(path);
        //    Assert.Single(dirInfo.GetDirectories());
        //    Assert.Single(dirInfo.GetFiles());
        //}
    }
}
