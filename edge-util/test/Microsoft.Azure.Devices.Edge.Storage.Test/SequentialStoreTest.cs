// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    public class SequentialStoreTest : SequentialStoreTestBase
    {
        readonly IStoreProvider storeProvider;

        public SequentialStoreTest()
        {
            this.storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
        }

        protected override IEntityStore<byte[], TV> GetEntityStore<TV>(string entityName)
            => this.storeProvider.GetEntityStore<byte[], TV>(entityName);
    }
}
