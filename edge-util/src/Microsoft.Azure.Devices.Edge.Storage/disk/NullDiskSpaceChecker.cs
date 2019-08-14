// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    public class NullDiskSpaceChecker : IDiskSpaceChecker
    {
        public void SetThresholdPercentage(int thresholdPercentage)
        {
        }

        public void SetMaxDiskUsageSize(long maxDiskUsageBytes)
        {
        }

        public void SetMaxMemoryUsageSize(IDbStoreStatistics dbStoreStatistics, bool usePersistentStorage, long maxUsageInBytes)
        {
        }

        public bool IsFull => false;
    }
}
