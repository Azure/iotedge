// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provices in memory implementation of a Db store
    /// </summary>
    class InMemoryDbStore : IDbStore
    {
        readonly ItemKeyedCollection keyValues;
        readonly ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();

        public InMemoryDbStore()
        {
            this.keyValues = new ItemKeyedCollection(new ByteArrayComparer());
        }

        public Task<bool> Contains(byte[] key) => Task.FromResult(this.keyValues.Contains(key));

        public Task<Option<byte[]>> Get(byte[] key)
        {
            this.listLock.EnterReadLock();
            try
            {
                Option<byte[]> value = this.keyValues.Contains(key)
                    ? Option.Some(this.keyValues[key].Value)
                    : Option.None<byte[]>();
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
            int i = 0;
            for (; i < snapshot.Count; i++)
            {
                byte[] key = snapshot[i].key;
                if (key.SequenceEqual(startKey))
                {
                    break;
                }
            }
            return this.IterateBatch(snapshot, i, batchSize, callback);
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
                Option<(byte[], byte[])> firstEntry = this.keyValues.Count > 0
                    ? Option.Some((this.keyValues[0].Key, this.keyValues[0].Value))
                    : Option.None<(byte[], byte[])>();
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
                Option<(byte[], byte[])> lastEntry = (this.keyValues.Count > 0)
                    ? Option.Some((this.keyValues[this.keyValues.Count - 1].Key, this.keyValues[this.keyValues.Count - 1].Value))
                    : Option.None<(byte[], byte[])>();
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
                if (!this.keyValues.Contains(key))
                {
                    this.keyValues.Add(new Item(key, value));
                }
                else
                {
                    this.keyValues[key].Value = value;
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
                this.keyValues.Remove(key);
                return Task.CompletedTask;
            }
            finally
            {
                this.listLock.ExitWriteLock();
            }
        }

        List<(byte[], byte[])> GetSnapshot()
        {
            this.listLock.EnterReadLock();
            try
            {
                return new List<(byte[], byte[])>(this.keyValues.ItemList);
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

        class ItemKeyedCollection : KeyedCollection<byte[], Item>
        {
            public ItemKeyedCollection(IEqualityComparer<byte[]> keyEqualityComparer)
                : base(keyEqualityComparer)
            {
            }

            protected override byte[] GetKeyForItem(Item item) => item.Key;

            public IList<(byte[], byte[])> ItemList => this.Items
                .Select(i => (i.Key, i.Value))
                .ToList();
        }

        class Item
        {
            public Item(byte[] key, byte[] value)
            {
                this.Key = Preconditions.CheckNotNull(key, nameof(key));
                this.Value = Preconditions.CheckNotNull(value, nameof(value));
            }

            public byte[] Key { get; }

            public byte[] Value { get; set; }
        }

        class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y) => (x == null && y == null) || x.SequenceEqual(y);

            public int GetHashCode(byte[] obj)
            {
                int hashCode = 1291371069;
                foreach (byte b in obj)
                {
                    hashCode = hashCode * -1521134295 + b.GetHashCode();
                }
                return hashCode;
            }
        }
    }
}
