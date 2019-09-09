// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;

    class FixedSizeDiskSpaceChecker : DiskSpaceCheckerBase
    {
        readonly string storageFolder;
        readonly long maxSizeBytes;

        public FixedSizeDiskSpaceChecker(string storageFolder, long maxSizeBytes, TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
            this.storageFolder = storageFolder;
            this.maxSizeBytes = maxSizeBytes;
            logger?.LogInformation($"Created fixed size space checker with max capacity of {maxSizeBytes} bytes on folder {storageFolder}");
        }

        protected override DiskSpaceStatus GetDiskStatus()
        {
            long bytes = GetDirectorySize(this.storageFolder);
            double usagePercentage = (double)bytes * 100 / this.maxSizeBytes;
            DiskSpaceStatus diskStatus = GetDiskStatus(usagePercentage);
            if (diskStatus != DiskSpaceStatus.Available)
            {
                this.Logger?.LogWarning($"High disk usage detected - using {usagePercentage}% of {this.maxSizeBytes} bytes");
            }

            return diskStatus;
        }

        static DiskSpaceStatus GetDiskStatus(double usagePercentage)
        {
            if (usagePercentage < 90)
            {
                return DiskSpaceStatus.Available;
            }

            if (usagePercentage < 100)
            {
                return DiskSpaceStatus.Critical;
            }

            return DiskSpaceStatus.Full;
        }

        static long GetDirectorySize(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);
            return GetDirectorySize(directory);
        }

        static long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;

            // Get size for all files in directory
            FileInfo[] files = directory.GetFiles("*", SearchOption.AllDirectories);
            foreach (FileInfo file in files)
            {
                size += file.Length;
            }

            return size;
        }
    }
}
