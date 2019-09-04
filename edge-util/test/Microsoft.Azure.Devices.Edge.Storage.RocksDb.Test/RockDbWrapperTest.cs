// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using RocksDbSharp;
    using Xunit;

    [Unit]
    public class RocksDbWrapperTest : IDisposable
    {
        readonly string rocksDbFolder;
        readonly string rocksDbBackupFolder;

        public RocksDbWrapperTest()
        {
            string tempFolder = Path.GetTempPath();
            this.rocksDbFolder = Path.Combine(tempFolder, $"edgeTestDb{Guid.NewGuid()}");
            if (Directory.Exists(this.rocksDbFolder))
            {
                Directory.Delete(this.rocksDbFolder);
            }

            Directory.CreateDirectory(this.rocksDbFolder);

            this.rocksDbBackupFolder = Path.Combine(tempFolder, $"edgeTestBackupDb{Guid.NewGuid()}");
            if (Directory.Exists(this.rocksDbBackupFolder))
            {
                Directory.Delete(this.rocksDbBackupFolder);
            }

            Directory.CreateDirectory(this.rocksDbBackupFolder);
        }

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(this.rocksDbFolder) && Directory.Exists(this.rocksDbFolder))
            {
                Directory.Delete(this.rocksDbFolder, true);
            }

            if (!string.IsNullOrWhiteSpace(this.rocksDbBackupFolder) && Directory.Exists(this.rocksDbBackupFolder))
            {
                Directory.Delete(this.rocksDbBackupFolder, true);
            }
        }

        [Fact]
        public void CreateWithNullOptionThrowsAsync()
        {
            // Arrange
            ICollection<string> partitions = new List<string>();
            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => RocksDbWrapper.Create(null, "AnyPath", partitions, Option.None<string>(), false));
            partitions.Clear();
        }

        [Fact]
        public void CreateWithNullPathThrowsAsync()
        {
            // Arrange
            ICollection<string> partitions = new List<string>();
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() => RocksDbWrapper.Create(options, null, partitions, Option.None<string>(), false));
            partitions.Clear();
        }

        [Fact]
        public void CreateWithNullPartitionsPathThrowsAsync()
        {
            // Arrange
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => RocksDbWrapper.Create(options, "AnyPath", null, Option.None<string>(), false));
        }

        [Fact]
        public void CreateWithNoBackupDirectoryThrowsAsync()
        {
            // Arrange
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);
            ICollection<string> partitions = new List<string>();

            // Act
            // Assert
            Assert.Throws<ArgumentException>(() => RocksDbWrapper.Create(options, this.rocksDbFolder, partitions, Option.None<string>(), true));
        }

        [Fact]
        public void CreateWithEmptyBackupDirectoryThrowsAsync()
        {
            // Arrange
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);
            ICollection<string> partitions = new List<string>();

            // Act
            // Assert
            Assert.Throws<ArgumentException>(() => RocksDbWrapper.Create(options, this.rocksDbFolder, partitions, Option.Some(" "), true));
        }

        [Fact]
        public void CreateWithRestoreFromBackupAsync()
        {
            // Arrange
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);
            IEnumerable<string> partitions = new List<string> { "Partition1", "Partition2" };
            byte[] key = { 1 };
            byte[] value = { 2 };
            string partitionName = "Partition3";
            string tempFolder = Path.GetTempPath();
            string tempStorageFolder = Path.Combine(tempFolder, $"edgeTestDbTemp{Guid.NewGuid()}");
            IRocksDb originalRocksDb = null;
            IRocksDb newRocksDb = null;

            try
            {
                // Act
                originalRocksDb = RocksDbWrapper.Create(options, this.rocksDbFolder, partitions, Option.Some(this.rocksDbBackupFolder), true);
                var columnFamily = originalRocksDb.CreateColumnFamily(new ColumnFamilyOptions(), partitionName);
                originalRocksDb.Put(key, value, columnFamily);
                var columnFamilies = originalRocksDb.ListColumnFamilies();
                originalRocksDb.Close();

                // Create new storage folder directory.
                if (Directory.Exists(tempStorageFolder))
                {
                    Directory.Delete(tempStorageFolder);
                }

                Directory.CreateDirectory(tempStorageFolder);

                IEnumerable<string> newPartitions = new List<string> { "Partition4", "Partition5" };
                newRocksDb = RocksDbWrapper.Create(options, tempStorageFolder, newPartitions, Option.Some(this.rocksDbBackupFolder), true);
                IEnumerable<string> newColumnFamilies = newRocksDb.ListColumnFamilies();
                IEnumerable<string> expectedColumnFamilies = columnFamilies.Union(newPartitions);

                // Assert
                Assert.True(!expectedColumnFamilies.Except(newColumnFamilies).Any() && !newColumnFamilies.Except(expectedColumnFamilies).Any());

                columnFamily = newRocksDb.GetColumnFamily(partitionName);
                byte[] obtainedValue = newRocksDb.Get(key, columnFamily);
                Assert.Equal(obtainedValue, value);

                newRocksDb.Close();
            }
            finally
            {
                originalRocksDb?.Dispose();
                newRocksDb?.Dispose();

                if (Directory.Exists(tempStorageFolder))
                {
                    Directory.Delete(tempStorageFolder, true);
                }
            }
        }

        [Fact]
        public void CreateWithRestoreAttemptFromNoBackupAsync()
        {
            // Arrange
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);
            IEnumerable<string> partitions = new List<string> { "Partition1", "Partition2" };
            byte[] key = { 1 };
            byte[] value = { 2 };
            string partitionName = "Partition3";
            string tempFolder = Path.GetTempPath();
            string tempStorageFolder = Path.Combine(tempFolder, $"edgeTestDbTemp{Guid.NewGuid()}");
            IRocksDb originalRocksDb = null;
            IRocksDb newRocksDb = null;

            try
            {
                // Act
                originalRocksDb = RocksDbWrapper.Create(options, this.rocksDbFolder, partitions, Option.Some(this.rocksDbBackupFolder), true);
                var columnFamily = originalRocksDb.CreateColumnFamily(new ColumnFamilyOptions(), partitionName);
                originalRocksDb.Put(key, value, columnFamily);
                originalRocksDb.Close();

                // Delete all backups from the backup directory.
                string[] files = Directory.GetFiles(this.rocksDbBackupFolder, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    File.Delete(file);
                }

                // Create new storage folder directory.
                if (Directory.Exists(tempStorageFolder))
                {
                    Directory.Delete(tempStorageFolder);
                }

                Directory.CreateDirectory(tempStorageFolder);

                IEnumerable<string> newPartitions = new List<string> { "Partition4", "Partition5" };
                newRocksDb = RocksDbWrapper.Create(options, tempStorageFolder, newPartitions, Option.Some(this.rocksDbBackupFolder), true);
                IEnumerable<string> newColumnFamilies = newRocksDb.ListColumnFamilies();

                // Assert
                // The column families retrieved should have all the new partitions specified during creation of the new and not
                // include the old partitions.
                Assert.True(newPartitions.All(x => newColumnFamilies.Contains(x)));
                Assert.True(newColumnFamilies.All(x => !partitions.Contains(x)));

                // The column family created earlier should not be present anymore.
                Assert.Throws<KeyNotFoundException>(() => newRocksDb.GetColumnFamily(partitionName));

                newRocksDb.Close();
            }
            finally
            {
                originalRocksDb?.Dispose();
                newRocksDb?.Dispose();

                if (Directory.Exists(tempStorageFolder))
                {
                    Directory.Delete(tempStorageFolder, true);
                }
            }
        }
    }
}
