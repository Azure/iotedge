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

        protected override bool GetIsDiskFull()
        {
            long bytes = GetDirectorySize(this.storageFolder);
            bool isFull = bytes >= this.maxSizeBytes;
            return isFull;
        }

        public FixedSizeSpaceChecker(string storageFolder, long maxSizeBytes, TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
            this.storageFolder = storageFolder;
            this.maxSizeBytes = maxSizeBytes;
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
