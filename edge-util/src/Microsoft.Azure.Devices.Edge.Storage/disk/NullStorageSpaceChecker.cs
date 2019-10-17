// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.Util;

namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetMaxStorageSize(long maxStorageBytes)
        {
        }

        public void SetMaxStorageSizeAndCheckFrequency(long maxStorageBytes, Option<int> checkFrequency)
        {
        }

        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer)
        {
        }

        public bool IsFull => false;
    }
}
