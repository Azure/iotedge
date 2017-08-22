// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StoreProvider : IStoreProvider
    {
        readonly IDbStoreProvider dbStoreProvider;

        public StoreProvider(IDbStoreProvider dbStoreProvider)
        {
            this.dbStoreProvider = Preconditions.CheckNotNull(dbStoreProvider, nameof(dbStoreProvider));
        }

        public IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName)
        {
            IDbStore entityDbStore = this.dbStoreProvider.GetDbStore(Preconditions.CheckNonWhiteSpace(entityName, nameof(entityName)));
            IEntityStore<TK, TV> entityStore = new EntityStore<TK, TV>(entityDbStore);
            return entityStore;
        }

        public async Task<ISequentialStore<T>> GetSequentialStore<T>(string entityName)
        {
            IEntityStore<byte[], T> underlyingStore = this.GetEntityStore<byte[], T>(entityName);
            ISequentialStore<T> sequentialStore = await SequentialStore<T>.Create(underlyingStore);
            return sequentialStore;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.dbStoreProvider?.Dispose();
            }
        }

        public void Dispose() => this.Dispose(true);
    }
}
