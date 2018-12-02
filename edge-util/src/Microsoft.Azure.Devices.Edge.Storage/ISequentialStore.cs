// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Store for storing entities in an ordered list - Entities can be retrieved in the same order in which they were saved.
    /// This can be used for implementing queues. 
    /// Each saved entity is associated with an offset, which can be used to retrieve the entity. 
    /// </summary>
    public interface ISequentialStore<T> : IDisposable
    {
        string EntityName { get; }

        Task<long> Append(T item);

        Task<bool> RemoveFirst(Func<long, T, Task<bool>> predicate);

        Task<IEnumerable<(long, T)>> GetBatch(long startingOffset, int batchSize);

        Task<long> Append(T item, CancellationToken cancellationToken);

        Task<bool> RemoveFirst(Func<long, T, Task<bool>> predicate, CancellationToken cancellationToken);

        Task<IEnumerable<(long, T)>> GetBatch(long startingOffset, int batchSize, CancellationToken cancellationToken);
    }
}
