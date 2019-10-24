// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IStorageSpaceChecker
    {
        void SetMaxSizeBytes(Option<long> maxSizeBytes);

        void SetStorageUsageComputer(Func<long> storageUsageComputer);

        bool IsFull { get; }
    }
}
