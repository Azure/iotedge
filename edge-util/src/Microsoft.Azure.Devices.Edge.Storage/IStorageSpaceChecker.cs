// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;

    public interface IStorageSpaceChecker
    {
        void SetMaxStorageSize(long maxStorageBytes);

        void SetStorageUsageComputer(Func<Task<long>> storageUsageComputer);

        bool IsFull { get; }
    }
}
