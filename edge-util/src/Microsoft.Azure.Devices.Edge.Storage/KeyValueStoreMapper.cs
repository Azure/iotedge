// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class KeyValueStoreMapper<TK, TK1, TV, TV1> : IKeyValueStore<TK, TV>
    {
        readonly IKeyValueStore<TK1, TV1> underlyingStore;
        readonly ITypeMapper<TK, TK1> keyMapper;
        readonly ITypeMapper<TV, TV1> valueMapper;

        public KeyValueStoreMapper(IKeyValueStore<TK1, TV1> underlyingStore, ITypeMapper<TK, TK1> keyMapper, ITypeMapper<TV, TV1> valueMapper)
        {
            this.underlyingStore = Preconditions.CheckNotNull(underlyingStore, nameof(underlyingStore));
            this.keyMapper = Preconditions.CheckNotNull(keyMapper, nameof(keyMapper));
            this.valueMapper = Preconditions.CheckNotNull(valueMapper, nameof(valueMapper));
        }

        public void Dispose()
        {
            this.underlyingStore?.Dispose();
        }

        public Task Put(TK key, TV value) => this.Put(key, value, CancellationToken.None);

        public Task<Option<TV>> Get(TK key) => this.Get(key, CancellationToken.None);

        public Task Remove(TK key) => this.Remove(key, CancellationToken.None);

        public Task<bool> Contains(TK key) => this.Contains(key, CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetFirstEntry() => this.GetFirstEntry(CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetLastEntry() => this.GetLastEntry(CancellationToken.None);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(batchSize, perEntityCallback, CancellationToken.None);

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(startKey, batchSize, perEntityCallback, CancellationToken.None);

        public Task Put(TK key, TV value, CancellationToken cancellationToken)
            => this.underlyingStore.Put(this.keyMapper.From(key), this.valueMapper.From(value), cancellationToken);

        public async Task<Option<TV>> Get(TK key, CancellationToken cancellationToken)
        {
            Option<TV1> valueTk1 = await this.underlyingStore.Get(this.keyMapper.From(key), cancellationToken);
            Option<TV> value = valueTk1.Map(this.valueMapper.To);
            return value;
        }

        public Task Remove(TK key, CancellationToken cancellationToken)
            => this.underlyingStore.Remove(this.keyMapper.From(key), cancellationToken);

        public Task<bool> Contains(TK key, CancellationToken cancellationToken)
            => this.underlyingStore.Contains(this.keyMapper.From(key), cancellationToken);

        public async Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            Option<(TK1 key, TV1 value)> firstEntry = await this.underlyingStore.GetFirstEntry(cancellationToken);
            return firstEntry.Map(e => (this.keyMapper.To(e.key), this.valueMapper.To(e.value)));
        }

        public async Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            Option<(TK1 key, TV1 value)> lastEntry = await this.underlyingStore.GetLastEntry(cancellationToken);
            return lastEntry.Map(e => (this.keyMapper.To(e.key), this.valueMapper.To(e.value)));
        }

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
            => this.IterateBatch(Option.Some(startKey), batchSize, callback, cancellationToken);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
            => this.IterateBatch(Option.None<TK>(), batchSize, callback, cancellationToken);

        public Task<ulong> Count() => this.underlyingStore.Count();

        public Task<ulong> GetCountFromOffset(TK offset) => this.underlyingStore.GetCountFromOffset(this.keyMapper.From(offset));

        Task IterateBatch(Option<TK> startKey, int batchSize, Func<TK, TV, Task> callback, CancellationToken cancellationToken)
        {
            Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
            Preconditions.CheckNotNull(callback, nameof(callback));

            Task DeserializingCallback(TK1 key, TV1 value)
                => callback(this.keyMapper.To(key), this.valueMapper.To(value));

            return startKey.Match(
                k => this.underlyingStore.IterateBatch(this.keyMapper.From(k), batchSize, DeserializingCallback, cancellationToken),
                () => this.underlyingStore.IterateBatch(batchSize, DeserializingCallback, cancellationToken));
        }
    }
}
