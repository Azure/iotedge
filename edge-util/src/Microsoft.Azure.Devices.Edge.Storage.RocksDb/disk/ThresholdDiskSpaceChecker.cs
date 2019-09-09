// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class ThresholdDiskSpaceChecker : DiskSpaceCheckerBase
    {
        readonly string drive;
        readonly double thresholdPercentage;

        public ThresholdDiskSpaceChecker(string drive, double thresholdPercentage, TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
            Preconditions.CheckArgument(thresholdPercentage >= 0 && thresholdPercentage <= 100, $"Invalid thresholdPercentage value {thresholdPercentage}");
            this.drive = Preconditions.CheckNonWhiteSpace(drive, nameof(drive));
            this.thresholdPercentage = thresholdPercentage;
            logger?.LogInformation($"Created threshold percentage disk space checker with threshold of {thresholdPercentage}% of drive {drive}");
        }

        protected override DiskSpaceStatus GetDiskStatus()
        {
            var driveInfo = new DriveInfo(this.drive);
            double percentDiskUsed = 100 - (double)driveInfo.AvailableFreeSpace * 100 / driveInfo.TotalSize;
            DiskSpaceStatus diskStatus = GetDiskStatus(percentDiskUsed, this.thresholdPercentage);
            if (diskStatus != DiskSpaceStatus.Available)
            {
                this.Logger?.LogWarning($"High disk usage detected - using {percentDiskUsed}% of a maximum of {this.thresholdPercentage}% of drive {this.drive}");
            }

            return diskStatus;
        }

        static DiskSpaceStatus GetDiskStatus(double percentDiskUsed, double thresholdPercentage)
        {
            double usagePercentage = percentDiskUsed * 100 / thresholdPercentage;
            if (usagePercentage < 85)
            {
                return DiskSpaceStatus.Available;
            }

            if (usagePercentage < 100)
            {
                return DiskSpaceStatus.Critical;
            }

            return DiskSpaceStatus.Full;
        }
    }
}
