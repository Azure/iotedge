// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using static Microsoft.Azure.Devices.Edge.Storage.MemorySpaceChecker;

    [Unit]
    public class MemorySpaceCheckerTest
    {
        [Fact]
        public async Task MemorySpaceCheckTest()
        {
            long maxStorageSize = 6 * 1024 * 1024;
            MemorySpaceChecker memorySpaceChecker = new MemorySpaceChecker(TimeSpan.FromSeconds(3), maxStorageSize, () => Task.FromResult(5 * 1024 * 1024L));

            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Unknown, memorySpaceChecker.UsageStatus);

            // Wait for the checker to run the first time.
            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Available, memorySpaceChecker.UsageStatus);

            memorySpaceChecker.SetStorageUsageComputer(() => Task.FromResult(maxStorageSize));
            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.True(memorySpaceChecker.IsFull);

            long newStorageSize = 8 * 1024 * 1024;
            memorySpaceChecker.SetMaxStorageSize(newStorageSize);

            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Available, memorySpaceChecker.UsageStatus);

            memorySpaceChecker.SetStorageUsageComputer(() => Task.FromResult((long)(newStorageSize * 0.95)));

            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Critical, memorySpaceChecker.UsageStatus);
        }
    }
}
