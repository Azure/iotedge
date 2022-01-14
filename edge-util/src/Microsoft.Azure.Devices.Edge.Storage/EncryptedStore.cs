// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class EncryptedStore<TK, TV> : IKeyValueStore<TK, TV>
    {
        readonly IKeyValueStore<TK, string> entityStore;

        public EncryptedStore(IKeyValueStore<TK, string> entityStore, IEncryptionProvider encryptionProvider)
        {
            this.entityStore = Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            this.EncryptionProvider = Preconditions.CheckNotNull(encryptionProvider, nameof(encryptionProvider));
        }

        protected IEncryptionProvider EncryptionProvider { get; }

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
            string encryptedString = await this.EncryptValue(valueString);
            await this.entityStore.Put(key, encryptedString, cancellationToken);
        }

        public async Task<Option<TV>> Get(TK key, CancellationToken cancellationToken)
        {
            Option<string> encryptedValue = await this.entityStore.Get(key, cancellationToken);
            return await encryptedValue.Map(
                    async e =>
                    {
                        Option<string> decryptedValue = await this.DecryptValue(e);
                        return decryptedValue.Map(d => d.FromJson<TV>());
                    })
                .GetOrElse(() => Task.FromResult(Option.None<TV>()));
        }

        public Task Remove(TK key, CancellationToken cancellationToken) => this.entityStore.Remove(key, cancellationToken);

        public async Task<bool> Contains(TK key, CancellationToken cancellationToken)
        {
            Option<TV> value = await this.Get(key, cancellationToken);
            return value.HasValue;
        }

        public async Task<Option<(TK key, TV value)>> GetFirstEntry(CancellationToken cancellationToken)
        {
            Option<(TK key, string value)> encryptedValue = await this.entityStore.GetFirstEntry(cancellationToken);
            return await encryptedValue.Map(
                    async e =>
                    {
                        Option<string> decryptedValue = await this.DecryptValue(e.value);
                        ILogger logger = Logger.Factory.CreateLogger<IKeyValueStore<TK, TV>>();
                        logger.LogInformation("Decrypted first");
                        return decryptedValue.Map(d => (e.key, d.FromJson<TV>()));
                    })
                .GetOrElse(() => Task.FromResult(Option.None<(TK key, TV value)>()));
        }

        public async Task<Option<(TK key, TV value)>> GetLastEntry(CancellationToken cancellationToken)
        {
            Option<(TK key, string value)> encryptedValue = await this.entityStore.GetLastEntry(cancellationToken);
            return await encryptedValue.Map(
                    async e =>
                    {
                        Option<string> decryptedValue = await this.DecryptValue(e.value);
                        ILogger logger = Logger.Factory.CreateLogger<IKeyValueStore<TK, TV>>();
                        logger.LogInformation("Decrypted last");
                        return decryptedValue.Map(d => (e.key, d.FromJson<TV>()));
                    })
                .GetOrElse(() => Task.FromResult(Option.None<(TK key, TV value)>()));
        }

        public Task IterateBatch(int batchSize, Func<TK, TV, Task> perEntityCallback, CancellationToken cancellationToken)
        {
            return this.entityStore.IterateBatch(
                batchSize,
                async (key, stringValue) =>
                {
                    Option<string> decryptedValue = await this.DecryptValue(stringValue);
                    await decryptedValue.ForEachAsync(
                        d =>
                        {
                            var value = d.FromJson<TV>();
                            return perEntityCallback(key, value);
                        });
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
                    Option<string> decryptedValue = await this.DecryptValue(stringValue);
                    await decryptedValue.ForEachAsync(
                        d =>
                        {
                            var value = d.FromJson<TV>();
                            return perEntityCallback(key, value);
                        });
                },
                cancellationToken);
        }

        public Task<ulong> Count() => this.entityStore.Count();

        public Task<ulong> GetCountFromOffset(TK offset) => this.entityStore.GetCountFromOffset(offset);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.entityStore?.Dispose();
            }
        }

        protected virtual Task<string> EncryptValue(string value)
            => this.EncryptionProvider.EncryptAsync(value);

        protected virtual async Task<Option<string>> DecryptValue(string encryptedValue)
        {
            try
            {
                string decryptedValue = await this.EncryptionProvider.DecryptAsync(encryptedValue);
                return Option.Some(decryptedValue);
            }
            catch (Exception)
            {
                return Option.None<string>();
            }
        }
    }
}
