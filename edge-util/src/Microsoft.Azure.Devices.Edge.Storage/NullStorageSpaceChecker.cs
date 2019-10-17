// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;

    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void Configure(long maxStorageBytes)
        {
        }

        public void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer)
        {
        }

        public bool IsFull => false;
    }
}
