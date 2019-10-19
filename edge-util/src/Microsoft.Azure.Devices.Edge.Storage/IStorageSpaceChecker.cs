// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IStorageSpaceChecker
    {
        void SetMaxSizeBytes(long maxSizeBytes);

        void SetCheckFrequency(Option<int> checkFrequencySecs);

        void DisableChecker();

        void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer);

        bool IsFull { get; }
    }
}
