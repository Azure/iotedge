// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A decorating wrapper over a DB store provider to provide ease of extending DB store provider behavior.
    /// </summary>
    public abstract class DbStoreProviderDecorator : IDbStoreProvider
    {
        static readonly ILogger LoggerInstance = Logger.Factory.CreateLogger("DbStoreProviderExt");
        protected IDbStoreProvider dbStoreProvider;

        public DbStoreProviderDecorator(IDbStoreProvider dbStoreProvider)
        {
            Preconditions.CheckNotNull(dbStoreProvider, nameof(dbStoreProvider));
            this.dbStoreProvider = dbStoreProvider;
        }

        protected ILogger Log
        {
            get { return LoggerInstance; }
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

        public Option<IDbStore> GetIfExistsDbStore(string partitionName)
        {
            return this.dbStoreProvider.GetIfExistsDbStore(partitionName);
        }

        public virtual IDbStore GetDbStore()
        {
            return this.dbStoreProvider.GetDbStore();
        }

        public virtual void RemoveDbStore(string partitionName)
        {
            this.dbStoreProvider.RemoveDbStore(partitionName);
        }

        public virtual void RemoveDbStore()
        {
            this.dbStoreProvider.RemoveDbStore();
        }
    }
}
