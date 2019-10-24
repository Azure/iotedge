// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetMaxSizeBytes(Option<long> maxStorageBytes)
        {
        }

        public void SetStorageUsageComputer(Func<long> storageUsageComputer)
        {
        }

        public bool IsFull => false;
    }
}
