using Microsoft.Azure.Devices.Edge.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Storage
{
    public abstract class DbStoreProviderDecorator : IDbStoreProvider
    {
        protected IDbStoreProvider dbStoreProvider;

        public DbStoreProviderDecorator(IDbStoreProvider dbStoreProvider)
        {
            Preconditions.CheckNotNull(dbStoreProvider, nameof(dbStoreProvider));
            this.dbStoreProvider = dbStoreProvider;
        }

        public virtual Task CloseAsync()
        {
            return this.dbStoreProvider.CloseAsync();
        }

        public void Dispose()
        {
            this.dbStoreProvider.Dispose();
        }

        public virtual IDbStore GetDbStore(string partitionName)
        {
            return this.dbStoreProvider.GetDbStore(partitionName);
        }

        public virtual IDbStore GetDbStore()
        {
            return this.dbStoreProvider.GetDbStore();
        }

        public virtual void RemoveDbStore(string partitionName)
        {
            this.dbStoreProvider.RemoveDbStore(partitionName);
        }

        public void RemoveDbStore()
        {
            this.dbStoreProvider.RemoveDbStore();
        }
    }
}
