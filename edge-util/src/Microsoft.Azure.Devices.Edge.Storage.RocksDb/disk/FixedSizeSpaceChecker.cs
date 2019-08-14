// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using Microsoft.Extensions.Logging;

    class FixedSizeSpaceChecker : DiskSpaceCheckerBase
    {
        readonly string storageFolder;
        readonly long maxSizeBytes;

        public FixedSizeSpaceChecker(string storageFolder, long maxSizeBytes, TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
            this.storageFolder = storageFolder;
            this.maxSizeBytes = maxSizeBytes;
            logger?.LogInformation($"Created fixed size space checker with max capacity of {maxSizeBytes} bytes on folder {storageFolder}");
        }

        protected override DiskStatus GetDiskStatus()
        {
            long bytes = DiskSpaceChecker.GetDirectorySize(this.storageFolder);
            double usagePercentage = (double)bytes * 100 / this.maxSizeBytes;
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
