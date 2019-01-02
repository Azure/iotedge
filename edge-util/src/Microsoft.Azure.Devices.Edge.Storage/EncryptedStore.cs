// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EncryptedStore<TK, TV> : IKeyValueStore<TK, TV>
    {
        readonly IKeyValueStore<TK, string> entityStore;
        readonly IEncryptionProvider encryptionProvider;

        public EncryptedStore(IKeyValueStore<TK, string> entityStore, IEncryptionProvider encryptionProvider)
        {
            this.entityStore = Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            this.encryptionProvider = Preconditions.CheckNotNull(encryptionProvider, nameof(encryptionProvider));
        }

        public Task Put(TK key, TV value) => this.Put(key, value, CancellationToken.None);

        public Task<Option<TV>> Get(TK key) => this.Get(key, CancellationToken.None);

        public Task Remove(TK key) => this.Remove(key, CancellationToken.None);

        public Task<bool> Contains(TK key) => this.Contains(key, CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetFirstEntry() => this.GetFirstEntry(CancellationToken.None);

        public Task<Option<(TK key, TV value)>> GetLastEntry() => this.GetLastEntry(CancellationToken.None);

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(batchSize, perEntityCallback, CancellationToken.None);

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback) => this.IterateBatch(startKey, batchSize, perEntityCallback, CancellationToken.None);

        public async Task Put(TK key, TV value, CancellationToken cancellationToken)
        {
            string valueString = value.ToJson();
            string encryptedString = await this.encryptionProvider.EncryptAsync(valueString);
            await this.entityStore.Put(key, encryptedString, cancellationToken);
        }

        public async Task<Option<TV>> Get(TK key, CancellationToken cancellationToken)
        {
            Option<string> encryptedValue = await this.entityStore.Get(key, cancellationToken);
            return await encryptedValue.Map(
                    async e =>
                    {
                        string decryptedValue = await this.encryptionProvider.DecryptAsync(e);
                        return Option.Some(decryptedValue.FromJson<TV>());
                    })
                .GetOrElse(() => Task.FromResult(Option.None<TV>()));
        }

        public Task Remove(TK key, CancellationToken cancellationToken) => this.entityStore.Remove(key, cancellationToken);

        public Task<bool> Contains(TK key, CancellationToken cancellationToken) => this.entityStore.Contains(key, cancellationToken);

        public async Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            Option<(TK key, string value)> encryptedValue = await this.entityStore.GetFirstEntry(cancellationToken);
            return await encryptedValue.Map(
                    async e =>
                    {
                        string decryptedValue = await this.encryptionProvider.DecryptAsync(e.value);
                        return Option.Some((e.key, decryptedValue.FromJson<TV>()));
                    })
                .GetOrElse(() => Task.FromResult(Option.None<(TK key, TV value)>()));
        }

        public async Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            Option<(TK key, string value)> encryptedValue = await this.entityStore.GetLastEntry(cancellationToken);
            return await encryptedValue.Map(
                    async e =>
                    {
                        string decryptedValue = await this.encryptionProvider.DecryptAsync(e.value);
                        return Option.Some((e.key, decryptedValue.FromJson<TV>()));
                    })
                .GetOrElse(() => Task.FromResult(Option.None<(TK key, TV value)>()));
        }

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken)
        {
            return this.entityStore.IterateBatch(
                batchSize,
                async (key, stringValue) =>
                {
                    string decryptedValue = await this.encryptionProvider.DecryptAsync(stringValue);
                    var value = decryptedValue.FromJson<TV>();
                    await perEntityCallback(key, value);
                },
                cancellationToken);
        }

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken)
        {
            return this.entityStore.IterateBatch(
                startKey,
                batchSize,
                async (key, stringValue) =>
                {
                    string decryptedValue = await this.encryptionProvider.DecryptAsync(stringValue);
                    var value = decryptedValue.FromJson<TV>();
                    await perEntityCallback(key, value);
                },
                cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.entityStore?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
