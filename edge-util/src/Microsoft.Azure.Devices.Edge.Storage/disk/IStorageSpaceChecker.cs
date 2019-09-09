// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    public interface IStorageSpaceChecker
    {
        void SetMaxStorageSize(long maxStorageBytes);

        bool IsFull { get; }
    }
}
