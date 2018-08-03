// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provices in memory implementation of a Db store
    /// </summary>
    class InMemoryDbStore : IDbStore
    {
        // Using a list instead of a dictionary becaues the dictionary is not ordered
        readonly List<(byte[], byte[])> keyValues;
        readonly ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();

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
            this.listLock.EnterReadLock();
            try
            {
                int index = this.GetIndex(key);
                Option<byte[]> value = index >= 0 ? Option.Some(this.keyValues[index].Item2) : Option.None<byte[]>();
                return Task.FromResult(value);
            }
            finally
            {
                this.listLock.ExitReadLock();
            }
        }

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> callback)
        {
            int index = 0;
            List<(byte[] key, byte[] value)> snapshot = this.GetSnapshot();
            return this.IterateBatch(snapshot, index, batchSize, callback);
        }

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> callback)
        {
            List<(byte[] key, byte[] value)> snapshot = this.GetSnapshot();
            int index = GetIndex(snapshot, startKey);
            return this.IterateBatch(snapshot, index, batchSize, callback);
        }

        async Task IterateBatch(List<(byte[] key, byte[] value)> snapshot, int index, int batchSize, Func<byte[], byte[], Task> callback)
        {
            if (index >= 0)
            {
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
            this.listLock.EnterReadLock();
            try
            {
                Option<(byte[], byte[])> firstEntry = this.keyValues.Count > 0 ? Option.Some(this.keyValues[0]) : Option.None<(byte[], byte[])>();
                return Task.FromResult(firstEntry);
            }
            finally
            {
                this.listLock.ExitReadLock();
            }
        }

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry()
        {
            this.listLock.EnterReadLock();
            try
            {
                Option<(byte[], byte[])> lastEntry = (this.keyValues.Count > 0) ? Option.Some(this.keyValues[this.keyValues.Count - 1]) : Option.None<(byte[], byte[])>();
                return Task.FromResult(lastEntry);
            }
            finally
            {
                this.listLock.ExitReadLock();
            }
        }

        public Task Put(byte[] key, byte[] value)
        {
            this.listLock.EnterWriteLock();
            try
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
            finally
            {
                this.listLock.ExitWriteLock();
            }
        }

        public Task Remove(byte[] key)
        {
            this.listLock.EnterWriteLock();
            try
            {
                int index = this.GetIndex(key);
                if (index >= 0)
                {
                    this.keyValues.RemoveAt(index);
                }
                return Task.CompletedTask;
            }
            finally
            {
                this.listLock.ExitWriteLock();
            }
        }

        internal int GetIndex(byte[] key) => GetIndex(this.keyValues, key);

        static int GetIndex(List<(byte[] key, byte[] value)> list, byte[] key)
        {
            for (int i = 0; i < list.Count; i++)
            {
                (byte[] key, byte[] value) kv = list[i];
                if (key.SequenceEqual(kv.key))
                {
                    return i;
                }
            }
            return -1;
        }

        List<(byte[], byte[])> GetSnapshot()
        {
            this.listLock.EnterReadLock();
            try
            {
                return new List<(byte[], byte[])>(this.keyValues);
            }
            finally
            {
                this.listLock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
