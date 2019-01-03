// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using Microsoft.Azure.Devices.Edge.Storage.Test;
    using Xunit;

    public class EntityStoreTest : EntityStoreTestBase, IClassFixture<TestRocksDbStoreProvider>
    {
        readonly TestRocksDbStoreProvider rocksDbStoreProvider;

        public EntityStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.rocksDbStoreProvider = rocksDbStoreProvider;
        }

        protected override IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName) => new EntityStore<TK, TV>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
    }
}
