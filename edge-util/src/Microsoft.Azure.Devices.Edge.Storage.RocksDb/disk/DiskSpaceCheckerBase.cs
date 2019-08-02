// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class DiskSpaceCheckerBase
    {
        readonly PeriodicTask diskSpaceChecker;
        readonly object updateLock = new object();

        DiskStatus diskStatus;

        protected DiskSpaceCheckerBase(TimeSpan checkFrequency, ILogger logger)
        {
            this.Logger = Preconditions.CheckNotNull(logger);
            this.diskSpaceChecker = new PeriodicTask(this.PeriodicTaskCallback, checkFrequency, checkFrequency, logger, "Disk space check");
        }

        public DiskStatus DiskStatus
        {
            get
            {
                // If disk status is Critical / Full, check disk status every time
                if (this.diskStatus > DiskStatus.Available)
                {
                    this.UpdateCurrentDiskSpaceStatus();
                }

                return this.diskStatus;
            }
        }

        protected ILogger Logger { get; }

        protected abstract DiskStatus GetDiskStatus();

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
