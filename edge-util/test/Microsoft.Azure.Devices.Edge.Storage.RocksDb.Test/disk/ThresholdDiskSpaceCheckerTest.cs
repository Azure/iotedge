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
                DriveInfo driveInfo = DiskSpaceChecker.GetMatchingDrive(testStorageFolder)
                    .Expect(() => new ArgumentException("Should find drive for temp folder"));
                double thresholdPercentage = 100 - ((double)driveInfo.AvailableFreeSpace - (50 * 1024 * 1024)) * 100 / driveInfo.TotalSize;
                var thresholdDiskSpaceChecker = new ThresholdDiskSpaceChecker(testStorageFolder, thresholdPercentage, TimeSpan.FromSeconds(3), logger);

                // Assert
                Assert.Equal(DiskSpaceStatus.Unknown, thresholdDiskSpaceChecker.DiskStatus);
                await Task.Delay(TimeSpan.FromSeconds(4));
                DiskSpaceStatus currentDiskStatus = thresholdDiskSpaceChecker.DiskStatus;
                Assert.True(currentDiskStatus == DiskSpaceStatus.Available || currentDiskStatus == DiskSpaceStatus.Critical);

                // Act
                string filePath = Path.Combine(testStorageFolder, "file0");
                string dummyFileContents = new string('*', 5 * 1024 * 1024);
                while (true)
                {
                    await File.AppendAllTextAsync(filePath, dummyFileContents);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    DiskSpaceStatus diskStatus = thresholdDiskSpaceChecker.DiskStatus;
                    if (diskStatus > DiskSpaceStatus.Available)
                    {
                        double percentDiskUsed = 100 - (double)driveInfo.AvailableFreeSpace * 100 / driveInfo.TotalSize;
                        double usagePercentage = percentDiskUsed * 100 / thresholdPercentage;
                        if (diskStatus == DiskSpaceStatus.Critical)
                        {
                            Assert.True(usagePercentage >= 85);
                            Assert.True(usagePercentage < 100);
                        }
                        else if (diskStatus == DiskSpaceStatus.Full)
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
                Assert.True(currentDiskStatus == DiskSpaceStatus.Available || currentDiskStatus == DiskSpaceStatus.Critical);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }
    }
}
