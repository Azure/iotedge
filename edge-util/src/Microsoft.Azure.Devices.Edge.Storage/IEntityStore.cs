// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Store for particular Key/Value pair. Implements PutOrUpdate and FindOrPut functionality on top of the KeyValueStore.
    /// </summary>
    public interface IEntityStore<TK, TV> : IKeyValueStore<TK, TV>
    {
        string EntityName { get; }

        Task<bool> Remove(TK key, Func<TV, bool> predicate, CancellationToken cancellationToken);

        Task<TV> Update(TK key, Func<TV, TV> updator, CancellationToken cancellationToken);

        Task<TV> PutOrUpdate(TK key, TV putValue, Func<TV, TV> valueUpdator, CancellationToken cancellationToken);

        Task<TV> FindOrPut(TK key, TV putValue, CancellationToken cancellationToken);
    }
}
