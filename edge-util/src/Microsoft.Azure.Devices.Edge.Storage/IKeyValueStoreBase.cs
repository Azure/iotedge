//// Copyright (c) Microsoft. All rights reserved.
//namespace Microsoft.Azure.Devices.Edge.Storage
//{
//    using System;
//    using System.Threading;
//    using System.Threading.Tasks;
//    using Microsoft.Azure.Devices.Edge.Util;

//    /// <summary>
//    /// Provides basic KeyValue store functionality.
//    /// </summary>
//    public interface IKeyValueStoreBase<TK, TV> : IDisposable
//    {
//        Task Put(TK key, TV value, CancellationToken cancellationToken);

//        Task<Option<TV>> Get(TK key, CancellationToken cancellationToken);

//        Task Remove(TK key, CancellationToken cancellationToken);

//        Task<bool> Contains(TK key, CancellationToken cancellationToken);

//        Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken);

//        Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken);

//        Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken);

//        Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken);
//    }
//}
