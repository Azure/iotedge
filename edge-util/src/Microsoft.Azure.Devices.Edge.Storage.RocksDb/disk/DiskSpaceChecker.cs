// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.Disk;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DiskSpaceChecker : IStorageSpaceChecker
    {
        const int CheckFrequencyDefault = 120;
        readonly string storageFolder;
        readonly object updateLock = new object();
        readonly int checkFrequency;
        Option<DiskSpaceCheckerBase> inner;

        DiskSpaceChecker(string storageFolder, Option<DiskSpaceCheckerBase> inner, int checkFrequency)
        {
            this.checkFrequency = checkFrequency;
            this.storageFolder = Preconditions.CheckNonWhiteSpace(storageFolder, nameof(storageFolder));
            this.inner = Preconditions.CheckNotNull(inner, nameof(inner));
        }

        // Returns false if checker is disabled (i.e. Option.HasValue is false)
        public bool IsFull => this.inner.Match<bool>(b => b.DiskStatus == DiskSpaceStatus.Full, () => false);

        public static DiskSpaceChecker Create(string storageFolder, Option<int> checkFrequency)
        {
            // Start up diskSpaceChecker unfilled - so that it will be enabled when SetMaxSizeBytes is called
            var diskSpaceChecker = new DiskSpaceChecker(storageFolder, Option.None<DiskSpaceCheckerBase>(), checkFrequency.GetOrElse(CheckFrequencyDefault));
            return diskSpaceChecker;
        }

        public void SetMaxSizeBytes(Option<long> maxSizeBytes)
        {
            if (maxSizeBytes.HasValue)
            {
                maxSizeBytes.ForEach(size =>
                {
                    this.inner.ForEach(b => b.Dispose());
                    this.inner = Option.Some(new FixedSizeDiskSpaceChecker(this.storageFolder, size, TimeSpan.FromSeconds(this.checkFrequency), Events.Log) as DiskSpaceCheckerBase);
                    Events.SetMaxSizeDiskSpaceUsage(size, this.storageFolder);
                });
            }
            else
            {
                this.DisableChecker();
            }
        }

        public void DisableChecker()
        {
            lock (this.updateLock)
            {
                if (this.inner.HasValue)
                {
                    this.inner.ForEach(b => b.Dispose());
                    this.inner = Option.None<DiskSpaceCheckerBase>();
                }
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
            return Option.Some(match);
        }

        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer)
        {
            throw new NotImplementedException();
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<DiskSpaceChecker>();

            enum EventIds
            {
                SetMaxSizeDiskSpaceUsage,
                SetCheckFrequency,
                SetMaxPercentageUsage,
                FoundDrive,
                NoMatchingDriveFound,
                ErrorGettingMatchingDrive
            }

            public static void SetMaxSizeDiskSpaceUsage(long maxSizeBytes, string storageFolder)
            {
                Log.LogInformation((int)EventIds.SetMaxSizeDiskSpaceUsage, $"Setting maximum disk space usage to {maxSizeBytes} bytes for folder {storageFolder} and started disk space usage checker");
            }

            public static void SetCheckFrequency(int checkFrequency, string storageFolder)
            {
                Log.LogInformation((int)EventIds.SetCheckFrequency, $"Setting check frequency to {checkFrequency} seconds for folder {storageFolder} and started disk space usage checker");
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
