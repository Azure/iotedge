// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EncryptedStore<TK, TV> : IEncryptedStore<TK, TV>
    {
        IKeyValueStore<TK, string> entityStore;
        IEncryptionProvider encryptionProvider;

        public EncryptedStore(IKeyValueStore<TK, string> entityStore, IEncryptionProvider encryptionProvider)
        {
            this.entityStore = Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            this.encryptionProvider = Preconditions.CheckNotNull(encryptionProvider, nameof(encryptionProvider));
        }

        public void Dispose()
        {
            this.entityStore?.Dispose();
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

        public async Task Remove(TK key)
        {
            await this.entityStore.Remove(key);
        }

        public async Task<bool> Contains(TK key)
        {
            return await this.entityStore.Contains(key);
        }

        public async Task<Option<(TK key, TV value)>> GetFirstEntry()
        {
            Option < (TK key, string value)> encryptedValue = await this.entityStore.GetFirstEntry();
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

        public async Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback)
        {
            await this.entityStore.IterateBatch(
                batchSize,
                async (key, stringValue) =>
                {
                    string decryptedValue = await this.encryptionProvider.DecryptAsync(stringValue);
                    var value = decryptedValue.FromJson<TV>();
                    await perEntityCallback(key, value);
                });
        }

        public async Task IterateBatch(TK startKey, int batchSize, Func<TK, TV, Task> perEntityCallback)
        {
            await this.entityStore.IterateBatch(
                startKey,
                batchSize,
                async (key, stringValue) =>
                {
                    string decryptedValue = await this.encryptionProvider.DecryptAsync(stringValue);
                    var value = decryptedValue.FromJson<TV>();
                    await perEntityCallback(key, value);
                });
        }
    }
}
