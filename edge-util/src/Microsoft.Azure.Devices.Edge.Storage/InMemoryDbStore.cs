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
    using Nito.AsyncEx;

    /// <summary>
    /// Provides an in memory implementation of the IDbStore.
    /// </summary>
    class InMemoryDbStore : IDbStore
    {
        readonly ItemKeyedCollection keyValues;
        readonly AsyncReaderWriterLock listLock = new AsyncReaderWriterLock();

        public InMemoryDbStore()
        {
            this.keyValues = new ItemKeyedCollection(new ByteArrayComparer());
        }

        public virtual Task Put(byte[] key, byte[] value) => this.Put(key, value, CancellationToken.None);

        public Task<Option<byte[]>> Get(byte[] key) => this.Get(key, CancellationToken.None);

        public Task Remove(byte[] key) => this.Remove(key, CancellationToken.None);

        public Task<bool> Contains(byte[] key) => this.Contains(key, CancellationToken.None);

        public Task<Option<(byte[] key, byte[] value)>> GetFirstEntry() => this.GetFirstEntry(CancellationToken.None);

        public Task<Option<(byte[] key, byte[] value)>> GetLastEntry() => this.GetLastEntry(CancellationToken.None);

        public Task IterateBatch(int batchSize, Func<byte[], byte[], Task> perEntityCallback) => this.IterateBatch(batchSize, perEntityCallback, CancellationToken.None);

        public Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> perEntityCallback) => this.IterateBatch(startKey, batchSize, perEntityCallback, CancellationToken.None);

        public Task<bool> Contains(byte[] key, CancellationToken cancellationToken) => Task.FromResult(this.keyValues.Contains(key));

        public async Task<Option<byte[]>> Get(byte[] key, CancellationToken cancellationToken)
        {
            using (await this.listLock.ReaderLockAsync(cancellationToken))
            {
                Option<byte[]> value = this.keyValues.Contains(key)
                    ? Option.Some(this.keyValues[key].Value)
                    : Option.None<byte[]>();
                return value;
            }
        }

        public async Task IterateBatch(int batchSize, Func<byte[], byte[], Task> callback, CancellationToken cancellationToken)
        {
            int index = 0;
            List<(byte[] key, byte[] value)> snapshot = await this.GetSnapshot(cancellationToken);
            await this.IterateBatch(snapshot, index, batchSize, callback, cancellationToken);
        }

        public async Task IterateBatch(byte[] startKey, int batchSize, Func<byte[], byte[], Task> callback, CancellationToken cancellationToken)
        {
            List<(byte[] key, byte[] value)> snapshot = await this.GetSnapshot(cancellationToken);
            int i = 0;
            for (; i < snapshot.Count; i++)
            {
                byte[] key = snapshot[i].key;
                if (key.SequenceEqual(startKey))
                {
                    break;
                }
            }

            await this.IterateBatch(snapshot, i, batchSize, callback, cancellationToken);
        }

        public async Task<Option<(byte[] key, byte[] value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            using (await this.listLock.ReaderLockAsync(cancellationToken))
            {
                Option<(byte[], byte[])> firstEntry = this.keyValues.Count > 0
                    ? Option.Some((this.keyValues[0].Key, this.keyValues[0].Value))
                    : Option.None<(byte[], byte[])>();
                return firstEntry;
            }
        }

        public async Task<Option<(byte[] key, byte[] value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            using (await this.listLock.ReaderLockAsync(cancellationToken))
            {
                Option<(byte[], byte[])> lastEntry = (this.keyValues.Count > 0)
                    ? Option.Some((this.keyValues[this.keyValues.Count - 1].Key, this.keyValues[this.keyValues.Count - 1].Value))
                    : Option.None<(byte[], byte[])>();
                return lastEntry;
            }
        }

        public virtual async Task Put(byte[] key, byte[] value, CancellationToken cancellationToken)
        {
            using (await this.listLock.WriterLockAsync(cancellationToken))
            {
                if (!this.keyValues.Contains(key))
                {
                    this.keyValues.Add(new Item(key, value));
                }
                else
                {
                    this.keyValues[key].Value = value;
                }
            }
        }

        public async Task Remove(byte[] key, CancellationToken cancellationToken)
        {
            using (await this.listLock.WriterLockAsync(cancellationToken))
            {
                this.keyValues.Remove(key);
            }
        }

        public void Dispose()
        {
            // No-op
        }

        public async Task<long> GetDbSizeInBytes()
        {
            long dbSize = 0;
            using (await this.listLock.ReaderLockAsync(CancellationToken.None))
            {
                foreach (Item keyValue in this.keyValues)
                {
                    dbSize += keyValue.Key.Length + keyValue.Value.Length;
                }
            }

            return dbSize;
        }

        async Task IterateBatch(List<(byte[] key, byte[] value)> snapshot, int index, int batchSize, Func<byte[], byte[], Task> callback, CancellationToken cancellationToken)
        {
            if (index >= 0)
            {
                for (int i = index; i < index + batchSize && i < snapshot.Count && !cancellationToken.IsCancellationRequested; i++)
                {
                    var keyClone = snapshot[i].key.Clone() as byte[];
                    var valueClone = snapshot[i].value.Clone() as byte[];
                    await callback(keyClone, valueClone);
                }
            }
        }

        async Task<List<(byte[], byte[])>> GetSnapshot(CancellationToken cancellationToken)
        {
            using (await this.listLock.ReaderLockAsync(cancellationToken))
            {
                return new List<(byte[], byte[])>(this.keyValues.ItemList);
            }
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

        class ItemKeyedCollection : KeyedCollection<byte[], Item>
        {
            public ItemKeyedCollection(IEqualityComparer<byte[]> keyEqualityComparer)
                : base(keyEqualityComparer)
            {
            }

            public IList<(byte[], byte[])> ItemList => this.Items
                .Select(i => (i.Key, i.Value))
                .ToList();

            protected override byte[] GetKeyForItem(Item item) => item.Key;
        }
    }
}
