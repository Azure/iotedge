// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IStorageSpaceChecker
    {
        void SetMaxSizeBytes(Option<long> maxStorageBytes);

        void SetStorageUsageComputer(Func<long> storageUsageComputer);

        bool IsFull { get; }
    }
}
