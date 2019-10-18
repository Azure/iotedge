// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetMaxStorageSize(long maxStorageBytes, Option<int> checkFrequency)
        {
        }

        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer)
        {
        }

        public bool IsFull => false;
    }
}
