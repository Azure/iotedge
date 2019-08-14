// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class FixedSizeMemorySpaceChecker : DiskSpaceCheckerBase
    {
        readonly IDbStoreStatistics dbStoreStatistics;
        readonly bool usePersistentStorage;
        readonly string storageFolder;
        readonly long maxSizeBytes;

        public FixedSizeMemorySpaceChecker(IDbStoreStatistics dbStoreStatistics, string storageFolder, bool usePersistentStorage, long maxSizeBytes, TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
            Preconditions.CheckNotNull(dbStoreStatistics, nameof(dbStoreStatistics));
            this.dbStoreStatistics = dbStoreStatistics;
            this.usePersistentStorage = usePersistentStorage;
            this.storageFolder = storageFolder;
            this.maxSizeBytes = maxSizeBytes;
            logger?.LogInformation($"Created fixed size memory space checker with max capacity of {maxSizeBytes} bytes on folder {storageFolder}");
        }

        protected override DiskStatus GetDiskStatus()
        {
            ulong totalBytesUsed = this.dbStoreStatistics.GetApproximateMemoryUsage();
            this.Logger?.LogInformation($"Bytes in memory {totalBytesUsed}");

            if (!this.usePersistentStorage)
            {
                totalBytesUsed += (ulong)DiskSpaceChecker.GetDirectorySize(this.storageFolder);
                this.Logger?.LogInformation($"Bytes in store {DiskSpaceChecker.GetDirectorySize(this.storageFolder)}");
            }

            double usagePercentage = (double)totalBytesUsed * 100 / this.maxSizeBytes;
            this.Logger?.LogInformation($"Usage percentage {usagePercentage}");

            DiskStatus diskStatus = GetDiskStatus(usagePercentage);
            if (diskStatus != DiskStatus.Available)
            {
                this.Logger?.LogWarning($"High disk usage detected - using {usagePercentage}% of {this.maxSizeBytes} bytes");
            }

            return diskStatus;
        }

        static DiskStatus GetDiskStatus(double usagePercentage)
        {
            if (usagePercentage < 90)
            {
                return DiskStatus.Available;
            }

            if (usagePercentage < 100)
            {
                return DiskStatus.Critical;
            }

            return DiskStatus.Full;
        }
    }
}
