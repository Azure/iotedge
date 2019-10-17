// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IStorageSpaceChecker
    {
        void SetMaxSize(long maxSize, Option<long> checkFrequency);

        void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer);

        bool IsFull { get; }
    }
}
