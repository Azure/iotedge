// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;

    class FixedSizeSpaceChecker : StorageSpaceCheckerBase
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

        protected override StorageStatus GetDiskStatus()
        {
            long bytes = GetDirectorySize(this.storageFolder);
            double usagePercentage = (double)bytes * 100 / this.maxSizeBytes;
            StorageStatus diskStatus = GetDiskStatus(usagePercentage);
            if (diskStatus != StorageStatus.Available)
            {
                this.Logger?.LogWarning($"High disk usage detected - using {usagePercentage}% of {this.maxSizeBytes} bytes");
            }

            return diskStatus;
        }

        static StorageStatus GetDiskStatus(double usagePercentage)
        {
            if (usagePercentage < 90)
            {
                return StorageStatus.Available;
            }

            if (usagePercentage < 100)
            {
                return StorageStatus.Critical;
            }

            return StorageStatus.Full;
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
