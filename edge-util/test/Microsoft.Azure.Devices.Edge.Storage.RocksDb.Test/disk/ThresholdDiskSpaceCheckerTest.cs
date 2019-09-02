// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test.Disk
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [Unit]
    public class ThresholdDiskSpaceCheckerTest
    {
        [Fact]
        public async Task TestStorageCheckTest()
        {
            // Arrange
            string tempFolder = Path.GetTempPath();
            string testStorageFolder = Path.Combine(tempFolder, $"edgeTestDb{Guid.NewGuid()}");
            if (Directory.Exists(testStorageFolder))
            {
                Directory.Delete(testStorageFolder);
            }

            Directory.CreateDirectory(testStorageFolder);
            var logger = Mock.Of<ILogger>();

            try
            {
                DriveInfo driveInfo = StorageSpaceChecker.GetMatchingDrive(testStorageFolder)
                    .Expect(() => new ArgumentException("Should find drive for temp folder"));
                double thresholdPercentage = 100 - ((double)driveInfo.AvailableFreeSpace - (50 * 1024 * 1024)) * 100 / driveInfo.TotalSize;
                var thresholdDiskSpaceChecker = new ThresholdStorageSpaceChecker(testStorageFolder, thresholdPercentage, TimeSpan.FromSeconds(3), logger);

                // Assert
                Assert.Equal(StorageStatus.Unknown, thresholdDiskSpaceChecker.DiskStatus);
                await Task.Delay(TimeSpan.FromSeconds(4));
                StorageStatus currentDiskStatus = thresholdDiskSpaceChecker.DiskStatus;
                Assert.True(currentDiskStatus == StorageStatus.Available || currentDiskStatus == StorageStatus.Critical);

                // Act
                string filePath = Path.Combine(testStorageFolder, "file0");
                string dummyFileContents = new string('*', 5 * 1024 * 1024);
                while (true)
                {
                    await File.AppendAllTextAsync(filePath, dummyFileContents);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    StorageStatus diskStatus = thresholdDiskSpaceChecker.DiskStatus;
                    if (diskStatus > StorageStatus.Available)
                    {
                        double percentDiskUsed = 100 - (double)driveInfo.AvailableFreeSpace * 100 / driveInfo.TotalSize;
                        double usagePercentage = percentDiskUsed * 100 / thresholdPercentage;
                        if (diskStatus == StorageStatus.Critical)
                        {
                            Assert.True(usagePercentage >= 85);
                            Assert.True(usagePercentage < 100);
                        }
                        else if (diskStatus == StorageStatus.Full)
                        {
                            Assert.True(usagePercentage >= 100);
                            break;
                        }
                    }
                }

                // Act
                File.Delete(filePath);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(4));
                currentDiskStatus = thresholdDiskSpaceChecker.DiskStatus;
                Assert.True(currentDiskStatus == StorageStatus.Available || currentDiskStatus == StorageStatus.Critical);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }
    }
}
