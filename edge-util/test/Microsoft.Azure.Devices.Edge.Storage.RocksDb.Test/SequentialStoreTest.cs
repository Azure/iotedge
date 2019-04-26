// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using Microsoft.Azure.Devices.Edge.Storage.Test;
    using Xunit;

    public class SequentialStoreTest : SequentialStoreTestBase, IClassFixture<TestRocksDbStoreProvider>
    {
        readonly IStoreProvider storeProvider;

        public SequentialStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.storeProvider = new StoreProvider(rocksDbStoreProvider);
        }

        protected override IEntityStore<byte[], TV> GetEntityStore<TV>(string entityName)
            => this.storeProvider.GetEntityStore<byte[], TV>(entityName);
    }
}
