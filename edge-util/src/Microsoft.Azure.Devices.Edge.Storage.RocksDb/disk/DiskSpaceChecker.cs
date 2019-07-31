// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.Disk;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    public class DiskSpaceChecker : IDiskSpaceChecker
    {
        readonly string storageFolder;
        readonly string drive;
        readonly TimeSpan checkFrequency;
        readonly object updateLock = new object();
        readonly IMessageStore messageStore;

        DiskSpaceCheckerBase inner;

        DiskSpaceChecker(string storageFolder, string drive, TimeSpan checkFrequency, DiskSpaceCheckerBase inner)
        {
            this.storageFolder = Preconditions.CheckNonWhiteSpace(storageFolder, nameof(storageFolder));
            this.drive = Preconditions.CheckNonWhiteSpace(drive, nameof(drive));
            this.checkFrequency = checkFrequency;
            this.inner = Preconditions.CheckNotNull(inner, nameof(inner));
        }

        public static DiskSpaceChecker Create(string storageFolder, int thresholdPercentage, TimeSpan checkFrequency)
        {
            string drive = GetMatchingDrive(storageFolder);
            var inner = new ThresholdDiskSpaceChecker(drive, thresholdPercentage, checkFrequency, Events.Log);
            var diskSpaceChecker = new DiskSpaceChecker(storageFolder, drive, checkFrequency, inner);
            return diskSpaceChecker;
        }

        public void SetThresholdPercentage(int thresholdPercentage)
        {
            lock (this.updateLock)
            {
                this.inner = new ThresholdDiskSpaceChecker(this.drive, thresholdPercentage, this.checkFrequency, Events.Log);
            }
        }

        public void SetMaxDiskUsageSize(long maxDiskUsageBytes)
        {
            lock (this.updateLock)
            {
                this.inner = new FixedSizeSpaceChecker(this.storageFolder, maxDiskUsageBytes, this.checkFrequency, Events.Log);
            }
        }

        public bool IsFull => this.inner.DiskStatus == DiskStatus.Full;

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

        class CleanupProcessor
        {
            readonly DiskSpaceChecker diskSpaceChecker;
            readonly TimeSpan checkFrequency;
            readonly Task cleanupTask;
            readonly bool deleteOlderMessages;

            public CleanupProcessor(DiskSpaceChecker diskSpaceChecker, TimeSpan checkFrequency, bool deleteOlderMessages)
            {
                this.diskSpaceChecker = diskSpaceChecker;
                this.checkFrequency = checkFrequency;
                this.cleanupTask = this.InitCleanupProcessor();
                this.deleteOlderMessages = deleteOlderMessages;
            }

            async Task InitCleanupProcessor()
            {
                while (true)
                {
                    try
                    {
                        if (this.diskSpaceChecker.inner.DiskStatus > DiskStatus.Available)
                        {
                            if (this.deleteOlderMessages)
                            {
                                await this.diskSpaceChecker.messageStore.CleanupMessagesFromHead(15, CancellationToken.None);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    await Task.Delay(this.checkFrequency);
                }
            }
        }

        static class Events
        {
            const int IdStart = 50000;
            public static readonly ILogger Log = Logger.Factory.CreateLogger<DiskSpaceChecker>();
        }
    }
}
