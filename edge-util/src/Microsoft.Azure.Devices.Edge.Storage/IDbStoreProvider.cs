// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;

    /// <summary>
    /// Provides DB Key/Value store
    /// </summary>
    public interface IDbStoreProvider : IDisposable
    {
        IDbStore GetDbStore(string partitionName);

        IDbStore GetDbStore();

        void RemoveDbStore(string partitionName);
    }
}
