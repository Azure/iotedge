// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using static Microsoft.Azure.Devices.Edge.Storage.MemorySpaceChecker;

    [Unit]
    public class MemorySpaceCheckerTest
    {
        [Fact]
        public void InvalidInputTest()
        {
            Assert.Throws<ArgumentNullException>(() => new MemorySpaceChecker(null));

            MemorySpaceChecker memorySpaceChecker = new MemorySpaceChecker(() => 5 * 1024 * 1024L);
            Assert.Throws<ArgumentNullException>(() => memorySpaceChecker.SetStorageUsageComputer(null));
        }

        [Fact]
        public void MemorySpaceCheckTest()
        {
            long maxStorageValue = 6 * 1024 * 1024;
            Option<long> maxStorageSize = Option.Some(maxStorageValue);
            MemorySpaceChecker memorySpaceChecker = new MemorySpaceChecker(() => 5 * 1024 * 1024L);

            Assert.False(memorySpaceChecker.IsFull);
            memorySpaceChecker.SetMaxSizeBytes(maxStorageSize);

            Assert.Equal(MemoryUsageStatus.Unknown, memorySpaceChecker.UsageStatus);
            Assert.False(memorySpaceChecker.IsFull);

            // Memory status should be 'Available' after calling 'IsFull'
            Assert.Equal(MemoryUsageStatus.Available, memorySpaceChecker.UsageStatus);

            memorySpaceChecker.SetStorageUsageComputer(() => maxStorageValue);
            Assert.True(memorySpaceChecker.IsFull);

            long newStorageValue = 8 * 1024 * 1024;
            Option<long> newStorageSize = Option.Some(newStorageValue);
            memorySpaceChecker.SetMaxSizeBytes(newStorageSize);

            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Available, memorySpaceChecker.UsageStatus);

            memorySpaceChecker.SetStorageUsageComputer(() => (long)(newStorageValue * 0.95));

            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Critical, memorySpaceChecker.UsageStatus);
        }

        [Fact]
        public void SetMaxSizeBytesTest()
        {
            long maxStorageValue = 1 * 1024 * 1024;
            Option<long> maxStorageSize = Option.Some(maxStorageValue);
            MemorySpaceChecker memorySpaceChecker = new MemorySpaceChecker(() => 2 * 1024 * 1024L);

            memorySpaceChecker.SetMaxSizeBytes(maxStorageSize);
            Assert.True(memorySpaceChecker.IsFull);

            memorySpaceChecker.SetMaxSizeBytes(Option.None<long>());
            Assert.False(memorySpaceChecker.IsFull);
            Assert.Equal(MemoryUsageStatus.Unknown, memorySpaceChecker.UsageStatus);
        }
    }
}
