// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides stores that are higher level abstractions over the underlying key/value stores.
    /// </summary>
    public interface IStoreProvider : IDisposable
    {
        IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName);

        Task<ISequentialStore<T>> GetSequentialStore<T>(string entityName);
    }
}
