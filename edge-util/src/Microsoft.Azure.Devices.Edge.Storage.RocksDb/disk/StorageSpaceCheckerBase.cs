// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class StorageSpaceCheckerBase
    {
        readonly PeriodicTask storageSpaceChecker;
        readonly object updateLock = new object();

        StorageStatus diskStatus;

        protected StorageSpaceCheckerBase(TimeSpan checkFrequency, ILogger logger)
        {
            this.Logger = Preconditions.CheckNotNull(logger);
            this.storageSpaceChecker = new PeriodicTask(this.PeriodicTaskCallback, checkFrequency, checkFrequency, logger, "Disk space check");
        }

        public StorageStatus DiskStatus
        {
            get
            {
                // If disk status is Critical / Full, check disk status every time
                if (this.diskStatus > StorageStatus.Available)
                {
                    this.UpdateCurrentDiskSpaceStatus();
                }

                return this.diskStatus;
            }
        }

        protected ILogger Logger { get; }

        protected abstract StorageStatus GetDiskStatus();

        Task PeriodicTaskCallback()
        {
            this.UpdateCurrentDiskSpaceStatus();
            return Task.CompletedTask;
        }

        void UpdateCurrentDiskSpaceStatus()
        {
            try
            {
                lock (this.updateLock)
                {
                    this.diskStatus = this.GetDiskStatus();
                }
            }
            catch (Exception e)
            {
                this.Logger?.LogWarning(e, $"Error updating disk status");
            }
        }
    }
}
