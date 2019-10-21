// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetMaxSizeBytes(long maxStorageBytes)
        {
        }

        public void SetCheckFrequency(Option<int> checkFrequencySecs)
        {
        }

        public void DisableChecker()
        {
        }

        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer)
        {
        }

        public bool IsFull => false;
    }
}
