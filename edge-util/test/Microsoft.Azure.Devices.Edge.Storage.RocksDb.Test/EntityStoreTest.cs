// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using Microsoft.Azure.Devices.Edge.Storage.Test;
    using Xunit;

    public class EntityStoreTest : EntityStoreTestBase, IClassFixture<TestRocksDbStoreProvider>
    {
        readonly IStoreProvider storeProvider;

        public EntityStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.storeProvider = new StoreProvider(rocksDbStoreProvider);
        }

        protected override IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName)
            => this.storeProvider.GetEntityStore<TK, TV>(entityName);

        protected override IEntityStore<TK, TV> GetEntityStore<TK, TV>(string backwardCompatibilityEntityName, string entityName)
           => this.storeProvider.GetEntityStore<TK, TV>(backwardCompatibilityEntityName, entityName);
    }
}
