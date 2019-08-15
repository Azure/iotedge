// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    public interface IDiskSpaceChecker
    {
        void SetThresholdPercentage(int thresholdPercentage);

        void SetMaxDiskUsageSize(long maxDiskUsageBytes);

        void SetMaxMemoryUsageSize(IDbStoreProvider dbStoreStatistics, bool usePersistentStorage, long maxUsageInBytes);

        bool IsFull { get; }
    }
}
