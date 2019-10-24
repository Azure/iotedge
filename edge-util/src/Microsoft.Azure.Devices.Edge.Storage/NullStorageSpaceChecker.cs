// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetStorageUsageComputer(Func<long> storageUsageComputer)
        {
        }

        public void SetMaxSizeBytes(Option<long> maxSizeBytes)
        {
        }

        public bool IsFull => false;
    }
}
