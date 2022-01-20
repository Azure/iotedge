// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provides DB Key/Value store.
    /// </summary>
    public interface IDbStoreProvider : IDisposable
    {
        IDbStore GetDbStore(string partitionName);

        Option<IDbStore> GetIfExistsDbStore(string partitionName);

        IDbStore GetDbStore();

        void RemoveDbStore(string partitionName);

        void RemoveDbStore();

        void CleanupAllStorage(string path);

        Task CloseAsync();
    }
}
