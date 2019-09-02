// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    using System;
    using Microsoft.Extensions.Logging;

    public class NullStorageSpaceChecker : StorageSpaceCheckerBase
    {
        public NullStorageSpaceChecker(TimeSpan checkFrequency, ILogger logger)
            : base(checkFrequency, logger)
        {
        }

        protected override StorageStatus GetDiskStatus() => StorageStatus.Available;
    }
}
