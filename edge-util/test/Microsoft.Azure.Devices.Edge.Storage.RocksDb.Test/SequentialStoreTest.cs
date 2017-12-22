// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using Microsoft.Azure.Devices.Edge.Storage.Test;
    using Xunit;

    public class SequentialStoreTest : SequentialStoreTestBase, IClassFixture<TestRocksDbStoreProvider>
    {
        readonly TestRocksDbStoreProvider rocksDbStoreProvider;

        public SequentialStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.rocksDbStoreProvider = rocksDbStoreProvider;
        }

        protected override IEntityStore<byte[], TV> GetEntityStore<TV>(string entityName) => new EntityStore<byte[], TV>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
    }
}
