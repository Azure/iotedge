// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    /// <summary>
    /// Provides locks for keys. Keys are divided into n shards and there is one lock per shard. 
    /// This improves performance as keys from different shards are locked on separate locks
    /// </summary>
    public class AsyncLockProvider<T>
    {
        readonly AsyncLock[] locks;
        readonly int keyShardCount;

        public AsyncLockProvider(int keyShardCount)
        {
            if (keyShardCount <= 0)
            {
                throw new ArgumentException("KeyShardCount should be > 0");
            }
            this.keyShardCount = keyShardCount;
            this.locks = new AsyncLock[keyShardCount];
            for (int i = 0; i < keyShardCount; i++)
            {
                this.locks[i] = new AsyncLock();
            }
        }

        public AsyncLock GetLock(T key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            int index = Math.Abs(key.GetHashCode() % this.keyShardCount);
            return this.locks[index];
        }
    }
}
