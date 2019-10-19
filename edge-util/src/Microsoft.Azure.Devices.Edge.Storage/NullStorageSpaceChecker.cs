// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer)
        {
        }

        public void SetMaxSizeBytes(long maxSizeBytes)
        {
        }

        public void SetCheckFrequency(Option<int> checkFrequencySecs)
        {
        }

        public void DisableChecker()
        {
        }

        public bool IsFull => false;
    }
}
