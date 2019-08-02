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
    public class FixedSizeSpaceCheckerTest
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
                long maxBytes = 2 * 1024 * 1024;
                var fixedSizeSpaceChecker = new FixedSizeSpaceChecker(testStorageFolder, maxBytes, TimeSpan.FromSeconds(3), logger);

                var rand = new Random();

                // Assert
                Assert.Equal(DiskStatus.Unknown, fixedSizeSpaceChecker.DiskStatus);
                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.Equal(DiskStatus.Available, fixedSizeSpaceChecker.DiskStatus);

                // Act
                string filePath = Path.Combine(testStorageFolder, "file0");
                var buffer = new byte[1000 * 1024];
                rand.NextBytes(buffer);
                await File.WriteAllBytesAsync(filePath, buffer);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.Equal(DiskStatus.Available, fixedSizeSpaceChecker.DiskStatus);

                // Act
                filePath = Path.Combine(testStorageFolder, "file1");
                rand.NextBytes(buffer);
                await File.WriteAllBytesAsync(filePath, buffer);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.Equal(DiskStatus.Critical, fixedSizeSpaceChecker.DiskStatus);

                // Act
                filePath = Path.Combine(testStorageFolder, "file2");
                rand.NextBytes(buffer);
                await File.WriteAllBytesAsync(filePath, buffer);

                // Assert
                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.Equal(DiskStatus.Full, fixedSizeSpaceChecker.DiskStatus);
            }
            finally
            {
                Directory.Delete(testStorageFolder, true);
            }
        }
    }
}
