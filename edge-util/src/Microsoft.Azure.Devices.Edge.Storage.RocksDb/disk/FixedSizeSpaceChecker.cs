// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.IO;
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
            long bytes = GetDirectorySize(this.storageFolder);
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

        static long GetDirectorySize(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);
            return GetDirectorySize(directory);
        }

        static long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;

            // Get size for all files in directory
            FileInfo[] files = directory.GetFiles();
            foreach (FileInfo file in files)
            {
                size += file.Length;
            }

            // Recursively get size for all directories in current directory
            DirectoryInfo[] dis = directory.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += GetDirectorySize(di);
            }

            return size;
        }
    }
}
