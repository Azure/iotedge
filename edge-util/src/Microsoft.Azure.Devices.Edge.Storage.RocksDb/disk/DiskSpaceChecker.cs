// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Storage.Disk;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DiskSpaceChecker : IStorageSpaceChecker
    {
        readonly string storageFolder;
        readonly TimeSpan checkFrequency;
        readonly object updateLock = new object();
        DiskSpaceCheckerBase inner;

        DiskSpaceChecker(string storageFolder, TimeSpan checkFrequency, DiskSpaceCheckerBase inner)
        {
            this.storageFolder = Preconditions.CheckNonWhiteSpace(storageFolder, nameof(storageFolder));
            this.checkFrequency = checkFrequency;
            this.inner = Preconditions.CheckNotNull(inner, nameof(inner));
        }

        public bool IsFull => this.inner.DiskStatus == DiskSpaceStatus.Full;

        public static DiskSpaceChecker Create(string storageFolder, long maxStorageBytes, TimeSpan checkFrequency)
        {
            DiskSpaceCheckerBase inner = new FixedSizeDiskSpaceChecker(storageFolder, maxStorageBytes, checkFrequency, Events.Log);
            var diskSpaceChecker = new DiskSpaceChecker(storageFolder, checkFrequency, inner);
            Events.Created(storageFolder);
            return diskSpaceChecker;
        }

        public void SetMaxStorageSize(long maxStorageBytes)
        {
            lock (this.updateLock)
            {
                Events.SetMaxSizeDiskSpaceUsage(maxStorageBytes, this.storageFolder);
                this.inner = new FixedSizeDiskSpaceChecker(this.storageFolder, maxStorageBytes, this.checkFrequency, Events.Log);
            }
        }

        internal static Option<DriveInfo> GetMatchingDrive(string storageFolder)
        {
            // TODO - This does not work when a different volume is mounted to a folder in a Windows Container
            // Because of this, Threshold space checker is currently not used.
            DriveInfo match = null;
            var drives = new DriveInfo[0];
            try
            {
                drives = DriveInfo.GetDrives();
                int segmentCount = -1;
                foreach (DriveInfo drive in drives)
                {
                    if (drive.IsReady)
                    {
                        if (storageFolder.StartsWith(drive.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            string[] segments = drive.Name.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                            if (segments.Length > segmentCount)
                            {
                                segmentCount = segments.Length;
                                match = drive;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Events.ErrorGettingMatchingDrive(storageFolder, e);
            }

            if (match == null)
            {
                Events.NoMatchingDriveFound(drives, storageFolder);
                return Option.None<DriveInfo>();
            }

            Events.FoundDrive(storageFolder, match);
            return Option.Maybe(match);
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<DiskSpaceChecker>();
            const int IdStart = 50000;

            enum EventIds
            {
                Created = IdStart,
                SetMaxSizeDiskSpaceUsage,
                SetMaxPercentageUsage,
                FoundDrive,
                NoMatchingDriveFound,
                ErrorGettingMatchingDrive
            }

            public static void Created(string storageFolder)
            {
                Log.LogInformation((int)EventIds.Created, $"Created disk space usage checker for folder {storageFolder}");
            }

            public static void SetMaxSizeDiskSpaceUsage(long maxSizeBytes, string storageFolder)
            {
                Log.LogInformation((int)EventIds.SetMaxSizeDiskSpaceUsage, $"Setting maximum disk space usage to {maxSizeBytes} bytes for folder {storageFolder}");
            }

            public static void SetMaxPercentageUsage(int thresholdPercentage, string drive)
            {
                Log.LogInformation((int)EventIds.SetMaxPercentageUsage, $"Setting maximum usage to {thresholdPercentage}% for disk {drive}");
            }

            public static void FoundDrive(string storageFolder, DriveInfo drive)
            {
                Log.LogInformation((int)EventIds.FoundDrive, $"Found drive {drive} for storage folder {storageFolder}");
                Log.LogDebug((int)EventIds.FoundDrive, $"Drive {drive.Name} - IsReady = {drive.IsReady}, TotalFreeSpace = {drive.TotalFreeSpace} TotalSize = {drive.TotalSize}");
            }

            public static void NoMatchingDriveFound(DriveInfo[] drives, string storageFolder)
            {
                Log.LogInformation((int)EventIds.NoMatchingDriveFound, $"Found {drives.Length} drives, but no matching drive found for storage folder {storageFolder}");
                string driveNames = drives.Select(d => d.Name).Join(", ");
                Log.LogInformation((int)EventIds.NoMatchingDriveFound, $"Drives found - {driveNames}");
            }

            public static void ErrorGettingMatchingDrive(string storageFolder, Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorGettingMatchingDrive, exception, $"Error getting drive for storage folder {storageFolder}");
            }
        }
    }
}
