// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Store for particular Key/Value pair. Implements PutOrUpdate and FindOrPut functionality on top of the KeyValueStore.
    /// </summary>
    public interface IEntityStore<TK, TV> : IKeyValueStore<TK, TV>
    {
        Task<bool> Remove(TK key, Func<TV, bool> predicate);

        Task<bool> Update(TK key, Func<TV, TV> updator);

        Task PutOrUpdate(TK key, TV putValue, Func<TV, TV> valueUpdator);

        Task FindOrPut(TK key, TV putValue);
    }
}
