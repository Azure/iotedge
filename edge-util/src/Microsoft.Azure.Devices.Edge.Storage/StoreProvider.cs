// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StoreProvider : IStoreProvider
    {
        readonly IDbStoreProvider dbStoreProvider;
        readonly TimeSpan operationTimeout;

        public StoreProvider(IDbStoreProvider dbStoreProvider)
            : this(dbStoreProvider, TimeSpan.FromMinutes(2))
        {
            this.dbStoreProvider = Preconditions.CheckNotNull(dbStoreProvider, nameof(dbStoreProvider));
        }

        public StoreProvider(IDbStoreProvider dbStoreProvider, TimeSpan operationTimeout)
        {
            this.dbStoreProvider = Preconditions.CheckNotNull(dbStoreProvider, nameof(dbStoreProvider));
            this.operationTimeout = operationTimeout;
        }

        public IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName)
        {
            IDbStore entityDbStore = this.dbStoreProvider.GetDbStore(Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName)));
            return this.GetEntityStore<TK, TV>(entityName, entityDbStore);
        }

        public IEntityStore<TK, TV> GetEntityStore<TK, TV>(string backwardCompatibleEntityName, string entityName)
        {
            Preconditions.CheckNonWhiteSpace(backwardCompatibleEntityName, nameof(backwardCompatibleEntityName));
            Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName));

            var entityDbStore = this.dbStoreProvider.GetIfExistsDbStore(backwardCompatibleEntityName);
            return entityDbStore.Match(
                some: backwardCompatibleDbStore => this.GetEntityStore<TK, TV>(backwardCompatibleEntityName, backwardCompatibleDbStore),
                none: () =>
                {
                    var dbstore = this.dbStoreProvider.GetDbStore(entityName);
                    return this.GetEntityStore<TK, TV>(entityName, dbstore);
                });
        }

        public async Task<ISequentialStore<T>> GetSequentialStore<T>(string entityName)
        {
            IEntityStore<byte[], T> underlyingStore = this.GetEntityStore<byte[], T>(entityName);
            ISequentialStore<T> sequentialStore = await SequentialStore<T>.Create(underlyingStore, entityName);
            return sequentialStore;
        }

        public async Task<ISequentialStore<T>> GetSequentialStore<T>(string entityName, long defaultHeadOffset)
        {
            IEntityStore<byte[], T> underlyingStore = this.GetEntityStore<byte[], T>(entityName);
            ISequentialStore<T> sequentialStore = await SequentialStore<T>.Create(underlyingStore, entityName, defaultHeadOffset);
            return sequentialStore;
        }

        public Task RemoveStore<T>(ISequentialStore<T> sequentialStore)
        {
            this.dbStoreProvider.RemoveDbStore(sequentialStore.EntityName);
            return Task.CompletedTask;
        }

        public Task RemoveStore<TK, TV>(IEntityStore<TK, TV> entityStore)
        {
            this.dbStoreProvider.RemoveDbStore(entityStore.EntityName);
            return Task.CompletedTask;
        }

        public Task RebuildAllStores()
        {
            this.dbStoreProvider.RebuildAllStores();
            return Task.CompletedTask;
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.dbStoreProvider?.Dispose();
            }
        }

        IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName, IDbStore entityDbStore)
        {
            IKeyValueStore<TK, TV> dbStoreMapper = new KeyValueStoreMapper<TK, byte[], TV, byte[]>(entityDbStore, new BytesMapper<TK>(), new BytesMapper<TV>());
            IEntityStore<TK, TV> entityStore = new EntityStore<TK, TV>(dbStoreMapper, entityName, 12);
            IEntityStore<TK, TV> timedEntityStore = new TimedEntityStore<TK, TV>(entityStore, this.operationTimeout);
            return timedEntityStore;
        }
    }
}
