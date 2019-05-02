// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    abstract class DiskSpaceCheckerBase
    {
        readonly PeriodicTask diskSpaceChecker;
        readonly object updateLock = new object();

        protected DiskSpaceCheckerBase(TimeSpan checkFrequency, ILogger logger)
        {
            this.Logger = Preconditions.CheckNotNull(logger);
            this.diskSpaceChecker = new PeriodicTask(this.UpdateCurrentDiskSpaceStatus, checkFrequency, TimeSpan.Zero, logger, "Disk space check");
        }

        public bool IsFull { get; private set; }

        protected ILogger Logger { get; }

        protected abstract bool GetIsDiskFull();

        Task UpdateCurrentDiskSpaceStatus()
        {
            lock (this.updateLock)
            {
                this.IsFull = this.GetIsDiskFull();
            }

            return Task.CompletedTask;
        }
    }
}
