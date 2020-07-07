// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System;
    using System.Threading.Tasks;

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

        Task CloseAsync();
    }
}
