// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public interface IDiskSpaceChecker
    {
        bool IsFull { get; }
    }

    public class DiskSpaceChecker : IDiskSpaceChecker
    {
        readonly string drive;
        readonly int thresholdPercentage;
        readonly PeriodicTask diskSpaceChecker;
        readonly object updateLock = new object();
        readonly ILogger logger;

        bool isFull;

        DiskSpaceChecker(string drive, int thresholdPercentage, TimeSpan checkFrequency, ILogger logger)
        {
            this.drive = drive;
            this.thresholdPercentage = thresholdPercentage;
            this.logger = logger;
            this.isFull = false;
            this.diskSpaceChecker = new PeriodicTask(this.UpdateCurrentDiskSpace, checkFrequency, TimeSpan.Zero, logger, "Disk space check");
        }

        public bool IsFull => this.isFull;

        public static DiskSpaceChecker Create(string storageFolder, int thresholdPercentage, TimeSpan checkFrequency, ILogger logger)
        {
            logger = logger ?? Logger.Factory.CreateLogger<DiskSpaceChecker>();
            string drive = GetMatchingDrive(storageFolder);
            Console.WriteLine("Getting drive info.. ");
            DriveInfo driveInfo = new DriveInfo(drive);
            Console.WriteLine($"Got created Drive {driveInfo.Name} - IsReady = {driveInfo.IsReady}, TotalFreeSpace = {driveInfo.TotalFreeSpace} TotalSize = {driveInfo.TotalSize} - RootDir - {driveInfo.RootDirectory.FullName}");
            return new DiskSpaceChecker(drive, thresholdPercentage, checkFrequency, logger);
        }

        static string GetMatchingDrive(string storageFolder)
        {
            var drives = DriveInfo.GetDrives();
            Console.WriteLine($"Found {drives.Length} drives");
            string match = string.Empty;
            int segmentCount = -1;
            foreach (DriveInfo drive in drives)
            {
                if (drive.IsReady)
                {
                    Console.WriteLine($"Drive {drive.Name} - IsReady = {drive.IsReady}, TotalFreeSpace = {drive.TotalFreeSpace} TotalSize = {drive.TotalSize}");

                    if (storageFolder.StartsWith(drive.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        string[] segments = drive.Name.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length > segmentCount)
                        {
                            Console.WriteLine($"New match for {drive.Name} with segments {segments.Length}");
                            segmentCount = segments.Length;
                            match = drive.Name;
                        }
                    }
                }
            }

            Console.WriteLine($"Final match = {match}");
            return match;
        }

        Task UpdateCurrentDiskSpace()
        {
            var driveInfo = new DriveInfo(this.drive);
            Console.WriteLine($"Got created Drive {driveInfo.Name} - AvailablenFreeSpace = {driveInfo.AvailableFreeSpace} TotalSize = {driveInfo.TotalSize}");
            lock (this.updateLock)
            {
                double percentDiskFree = (double)driveInfo.AvailableFreeSpace * 100 / driveInfo.TotalFreeSpace;
                this.isFull = percentDiskFree <= this.thresholdPercentage;
                Console.WriteLine($"Percentage disk free = {percentDiskFree}, isFull = {this.isFull}");
            }

            return Task.CompletedTask;
        }

        //static long GetDirectorySize(string directoryPath)
        //{
        //    var directory = new DirectoryInfo(directoryPath);
        //    return GetDirectorySize(directory);
        //}

        //static long GetDirectorySize(DirectoryInfo directory)
        //{
        //    long size = 0;

        //    // Get size for all files in directory
        //    FileInfo[] files = directory.GetFiles();
        //    foreach (FileInfo file in files)
        //    {
        //        size += file.Length;
        //    }

        //    // Recursively get size for all directories in current directory
        //    DirectoryInfo[] dis = directory.GetDirectories();
        //    foreach (DirectoryInfo di in dis)
        //    {
        //        size += GetDirectorySize(di);
        //    }
        //    return size;
        //}

        static class Events
        {
            const int IdStart = 0;
            public static readonly ILogger Log = Logger.Factory.CreateLogger<DiskSpaceChecker>();
        }
    }
}
