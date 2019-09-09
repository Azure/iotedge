// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Disk
{
    public class NullStorageSpaceChecker : IStorageSpaceChecker
    {
        public void SetMaxStorageSize(long maxStorageBytes)
        {
        }

        public bool IsFull => false;
    }
}
