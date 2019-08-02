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
        readonly int thresholdPercentage;

        public ThresholdDiskSpaceChecker(string drive, int thresholdPercentage, TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
            Preconditions.CheckArgument(thresholdPercentage >= 0 && thresholdPercentage <= 100, $"Invalid thresholdPercentage value {thresholdPercentage}");
            this.drive = Preconditions.CheckNonWhiteSpace(drive, nameof(drive));
            this.thresholdPercentage = thresholdPercentage;
            logger?.LogInformation($"Created threshold percentage disk space checker with threshold of {thresholdPercentage}% of drive {drive}");
        }

        protected override DiskStatus GetDiskStatus()
        {
            var driveInfo = new DriveInfo(this.drive);
            double percentDiskFree = (double)driveInfo.AvailableFreeSpace * 100 / driveInfo.TotalFreeSpace;
            DiskStatus diskStatus = GetDiskStatus(percentDiskFree, this.thresholdPercentage);
            if (diskStatus != DiskStatus.Available)
            {
                this.Logger?.LogWarning($"High disk usage detected - using {percentDiskFree}% of a maximum of {this.thresholdPercentage}% of drive {this.drive}");
            }
            return diskStatus;
        }

        static DiskStatus GetDiskStatus(double percentDiskFree, int thresholdPercentage)
        {
            double usagePercentage = percentDiskFree * 100 / thresholdPercentage;
            if (usagePercentage < 85)
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
