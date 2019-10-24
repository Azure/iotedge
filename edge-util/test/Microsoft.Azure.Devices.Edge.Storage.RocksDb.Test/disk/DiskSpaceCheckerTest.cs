// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test.Disk
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DiskSpaceCheckerTest
    {
        readonly int delay = 6;
        readonly int checkFrequency = 3;
        [Fact]
        public async Task UpdateMaxSizeToZeroTest()
        {
            // Arrange
            string testStorageFolder = this.CreateTempStorageFolder();

            try
            {
                DriveInfo driveInfo = DiskSpaceChecker.GetMatchingDrive(testStorageFolder)
                    .Expect(() => new ArgumentException("Should find drive for temp folder"));
                long maxStorageSize = 0;
                DiskSpaceChecker diskSpaceChecker = DiskSpaceChecker.Create(testStorageFolder, Option.Some(this.checkFrequency));
                diskSpaceChecker.SetMaxSizeBytes(Option.Some(maxStorageSize));

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.True(diskSpaceChecker.IsFull);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }

        [Fact]
        public async Task SetMaxSizeToNoneTest()
        {
            // Arrange
            string testStorageFolder = this.CreateTempStorageFolder();

            try
            {
                DriveInfo driveInfo = DiskSpaceChecker.GetMatchingDrive(testStorageFolder)
                    .Expect(() => new ArgumentException("Should find drive for temp folder"));
                DiskSpaceChecker diskSpaceChecker = DiskSpaceChecker.Create(testStorageFolder, Option.Some(this.checkFrequency));
                diskSpaceChecker.SetMaxSizeBytes(Option.None<long>());

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.False(diskSpaceChecker.IsFull);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }

        [Fact]
        public async Task SmokeTest()
        {
            // Arrange
            string testStorageFolder = this.CreateTempStorageFolder();

            try
            {
                DriveInfo driveInfo = DiskSpaceChecker.GetMatchingDrive(testStorageFolder)
                    .Expect(() => new ArgumentException("Should find drive for temp folder"));
                long maxStorageSize = 6 * 1024 * 1024;
                DiskSpaceChecker diskSpaceChecker = DiskSpaceChecker.Create(testStorageFolder, Option.Some(this.checkFrequency));
                diskSpaceChecker.SetMaxSizeBytes(Option.Some(maxStorageSize));

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.False(diskSpaceChecker.IsFull);

                // Act
                string filePath = Path.Combine(testStorageFolder, "file0");
                string dummyFileContents = new string('*', 5 * 1024 * 1024);
                await File.AppendAllTextAsync(filePath, dummyFileContents);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.False(diskSpaceChecker.IsFull);

                // Act
                diskSpaceChecker.SetMaxSizeBytes(Option.Some((long)(4 * 1024 * 1024)));

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.True(diskSpaceChecker.IsFull);

                // Act
                diskSpaceChecker.SetMaxSizeBytes(Option.Some((long)(8 * 1024 * 1024)));

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.False(diskSpaceChecker.IsFull);

                // Act
                await File.AppendAllTextAsync(filePath, dummyFileContents);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.True(diskSpaceChecker.IsFull);

                // Act
                File.Delete(filePath);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.False(diskSpaceChecker.IsFull);

                await File.AppendAllTextAsync(filePath, dummyFileContents);
                diskSpaceChecker.SetMaxSizeBytes(Option.Some((long)(4 * 1024 * 1024)));

                await Task.Delay(TimeSpan.FromSeconds(this.delay));
                Assert.True(diskSpaceChecker.IsFull);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }

        private string CreateTempStorageFolder()
        {
            string tempFolder = Path.GetTempPath();
            string testStorageFolder = Path.Combine(tempFolder, $"edgeTestDb{Guid.NewGuid()}");
            if (Directory.Exists(testStorageFolder))
            {
                Directory.Delete(testStorageFolder);
            }

            Directory.CreateDirectory(testStorageFolder);
            return testStorageFolder;
        }
    }
}
