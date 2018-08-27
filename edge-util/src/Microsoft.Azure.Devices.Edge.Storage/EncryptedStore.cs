// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
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

        public async Task Put(TK key, TV value)
        {
            string valueString = value.ToJson();
            string encryptedString = await this.encryptionProvider.EncryptAsync(valueString);
            await this.entityStore.Put(key, encryptedString);
        }

        public async Task<Option<TV>> Get(TK key)
        {
            Option<string> encryptedValue = await this.entityStore.Get(key);
            return await encryptedValue.Map(
                    async e =>
                    {
                        string decryptedValue = await this.encryptionProvider.DecryptAsync(e);
                        return Option.Some(decryptedValue.FromJson<TV>());
                    })
                .GetOrElse(() => Task.FromResult(Option.None<TV>()));
        }

        public Task Remove(TK key) => this.entityStore.Remove(key);

        public Task<bool> Contains(TK key) => this.entityStore.Contains(key);

        public async Task<Option<(TK key, TV value)>> GetFirstEntry()
        {
            Option<(TK key, string value)> encryptedValue = await this.entityStore.GetFirstEntry();
            return await encryptedValue.Map(
                    async e =>
                    {
                        string decryptedValue = await this.encryptionProvider.DecryptAsync(e.value);
                        return Option.Some((e.key, decryptedValue.FromJson<TV>()));
                    })
                .GetOrElse(() => Task.FromResult(Option.None<(TK key, TV value)>()));
        }

        public async Task<Option<(TK key, TV value)>> GetLastEntry()
        {
            Option<(TK key, string value)> encryptedValue = await this.entityStore.GetLastEntry();
            return await encryptedValue.Map(
                    async e =>
                    {
                        string decryptedValue = await this.encryptionProvider.DecryptAsync(e.value);
                        return Option.Some((e.key, decryptedValue.FromJson<TV>()));
                    })
                .GetOrElse(() => Task.FromResult(Option.None<(TK key, TV value)>()));
        }

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback)
        {
            return this.entityStore.IterateBatch(
                batchSize,
                async (key, stringValue) =>
                {
                    string decryptedValue = await this.encryptionProvider.DecryptAsync(stringValue);
                    var value = decryptedValue.FromJson<TV>();
                    await perEntityCallback(key, value);
                });
        }

        public Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback)
        {
            return this.entityStore.IterateBatch(
                startKey,
                batchSize,
                async (key, stringValue) =>
                {
                    string decryptedValue = await this.encryptionProvider.DecryptAsync(stringValue);
                    var value = decryptedValue.FromJson<TV>();
                    await perEntityCallback(key, value);
                });
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
