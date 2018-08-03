// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provices in memory implementation of a Db store
    /// </summary>
    class InMemoryDbStore : IDbStore
    {
        // Deliberately using list (instead of a synchronied list like Concurrent dictionary) to 
        // better simulate acutal key-value stores like RocksDb.
        readonly List<(byte[], byte[])> keyValues;

        public InMemoryDbStore()
        {
            this.keyValues = new List<(byte[], byte[])>();
        }

        public Task<bool> Contains(byte[] key)
        {
            int index = this.GetIndex(key);
            return Task.FromResult(index >= 0);
        }

        public Task<Option<byte[]>> Get(byte[] key)
        {
            int index = this.GetIndex(key);
            Option<byte[]> value = index >= 0 ? Option.Some(this.keyValues[index].Item2) : Option.None<byte[]>();
            return Task.FromResult(value);
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> callback)
        {
            int index = this.keyValues.Count > 0 ? 0 : -1;
            return this.IterateBatch(index, batchSize, callback);
        }

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> callback)
        {
            int index = this.GetIndex(startKey);
            return this.IterateBatch(index, batchSize, callback);
        }

        async Task IterateBatch(int index, int batchSize, Func<byte[], byte[], Task> callback)
        {
            if (index >= 0)
            {
                for (int i = index; i < index + batchSize && i < this.keyValues.Count; i++)
                {
                    var keyClone = this.keyValues[i].Item1.Clone() as byte[];
                    var valueClone = this.keyValues[i].Item2.Clone() as byte[];
                    await callback(keyClone, valueClone);
                }
            }
        }

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry()
        {
            Option<(byte[], byte[])> firstEntry = (this.keyValues.Count > 0) ? Option.Some(this.keyValues[0]) : Option.None<(byte[], byte[])>();
            return Task.FromResult(firstEntry);
        }

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry()
        {
            Option<(byte[], byte[])> lastEntry = (this.keyValues.Count > 0) ? Option.Some(this.keyValues[this.keyValues.Count - 1]) : Option.None<(byte[], byte[])>();
            return Task.FromResult(lastEntry);
        }

        public Task Put(byte[] key, byte[] value)
        {
            int index = this.GetIndex(key);
            if (index < 0)
            {
                this.keyValues.Add((key, value));
            }
            else
            {
                this.keyValues[index] = (key, value);
            }
            return Task.CompletedTask;
        }

        public Task Remove(byte[] key)
        {
            int index = this.GetIndex(key);
            if (index >= 0)
            {
                this.keyValues.RemoveAt(index);
            }
            return Task.CompletedTask;
        }

        internal int GetIndex(byte[] key)
        {
            // Not locking here, to get similar behavior as the RocksDb based implementation.
            // The application code should to ensure that locking is not required.
            for (int i = 0; i < this.keyValues.Count; i++)
            {
                (byte[] key, byte[] value) kv = this.keyValues[i];
                if (key.SequenceEqual(kv.key))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
