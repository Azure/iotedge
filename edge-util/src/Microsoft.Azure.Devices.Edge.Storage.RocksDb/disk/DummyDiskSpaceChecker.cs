// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using Microsoft.Extensions.Logging;

    public class DummyDiskSpaceChecker : DiskSpaceCheckerBase
    {
        public DummyDiskSpaceChecker(TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
        }

        protected override DiskStatus GetDiskStatus() => DiskStatus.Available;
    }
}
