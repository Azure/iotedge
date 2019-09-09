// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test.Disk
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DiskSpaceCheckerTest
    {
        [Fact]
        public async Task SmokeTest()
        {
            // Arrange
            string tempFolder = Path.GetTempPath();
            string testStorageFolder = Path.Combine(tempFolder, $"edgeTestDb{Guid.NewGuid()}");
            if (Directory.Exists(testStorageFolder))
            {
                Directory.Delete(testStorageFolder);
            }

            Directory.CreateDirectory(testStorageFolder);

            try
            {
                DriveInfo driveInfo = DiskSpaceChecker.GetMatchingDrive(testStorageFolder)
                    .Expect(() => new ArgumentException("Should find drive for temp folder"));
                long maxStorageSize = 6 * 1024 * 1024;
                DiskSpaceChecker diskSpaceChecker = DiskSpaceChecker.Create(testStorageFolder, maxStorageSize, TimeSpan.FromSeconds(3));

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.False(diskSpaceChecker.IsFull);

                // Act
                string filePath = Path.Combine(testStorageFolder, "file0");
                string dummyFileContents = new string('*', 5 * 1024 * 1024);
                await File.AppendAllTextAsync(filePath, dummyFileContents);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.False(diskSpaceChecker.IsFull);

                // Act
                diskSpaceChecker.SetMaxStorageSize(4 * 1024 * 1024);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.True(diskSpaceChecker.IsFull);

                // Act
                diskSpaceChecker.SetMaxStorageSize(8 * 1024 * 1024);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.False(diskSpaceChecker.IsFull);

                // Act
                await File.AppendAllTextAsync(filePath, dummyFileContents);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.True(diskSpaceChecker.IsFull);

                // Act
                File.Delete(filePath);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.False(diskSpaceChecker.IsFull);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }
    }
}
