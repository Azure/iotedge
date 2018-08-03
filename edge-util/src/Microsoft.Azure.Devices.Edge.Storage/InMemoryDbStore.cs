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
        // Using a list instead of a dictionary becaues the dictionary is not ordered
        readonly List<(byte[], byte[])> keyValues;
        readonly object listLock = new object();

        public InMemoryDbStore()
        {
            this.keyValues = new List<(byte[], byte[])>();
        }

        public async Task<bool> Contains(byte[] key)
        {
            Option<byte[]> value = await this.Get(key);
            return value.HasValue;
        }

        public Task<Option<byte[]>> Get(byte[] key)
        {
            lock (this.listLock)
            {
                int index = this.GetIndex(key);
                Option<byte[]> value = index >= 0 ? Option.Some(this.keyValues[index].Item2) : Option.None<byte[]>();
                return Task.FromResult(value);
            }
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> callback)
        {
            int index = 0;
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
                List<(byte[] key, byte[] value)> snapshot = this.GetSnapshot();

                for (int i = index; i < index + batchSize && i < snapshot.Count; i++)
                {
                    var keyClone = snapshot[i].key.Clone() as byte[];
                    var valueClone = snapshot[i].value.Clone() as byte[];
                    await callback(keyClone, valueClone);
                }
            }
        }

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry()
        {
            lock (this.listLock)
            {
                Option<(byte[], byte[])> firstEntry = this.keyValues.Count > 0 ? Option.Some(this.keyValues[0]) : Option.None<(byte[], byte[])>();
                return Task.FromResult(firstEntry);
            }
        }

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry()
        {
            lock (this.listLock)
            {
                Option<(byte[], byte[])> lastEntry = (this.keyValues.Count > 0) ? Option.Some(this.keyValues[this.keyValues.Count - 1]) : Option.None<(byte[], byte[])>();
                return Task.FromResult(lastEntry);
            }
        }

        public Task Put(byte[] key, byte[] value)
        {
            lock (this.listLock)
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
        }

        public Task Remove(byte[] key)
        {
            lock (this.listLock)
            {
                int index = this.GetIndex(key);
                if (index >= 0)
                {
                    this.keyValues.RemoveAt(index);
                }
                return Task.CompletedTask;
            }
        }

        internal int GetIndex(byte[] key)
        {
            lock (this.listLock)
            {
                for (int i = 0; i < this.keyValues.Count; i++)
                {
                    (byte[] key, byte[] value) kv = this.keyValues[i];
                    if (key.SequenceEqual(kv.key))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        List<(byte[], byte[])> GetSnapshot()
        {
            lock (this.listLock)
            {
                return new List<(byte[], byte[])>(this.keyValues);
            }
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
