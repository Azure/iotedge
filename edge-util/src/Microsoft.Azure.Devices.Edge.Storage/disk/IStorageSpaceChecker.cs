// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    public interface IStorageSpaceChecker
    {
        void SetMaxSizeBytes(long maxStorageBytes);

        void SetCheckFrequency(Option<int> checkFrequencySecs);

        void DisableChecker();

        void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer);

        bool IsFull { get; }
    }
}
