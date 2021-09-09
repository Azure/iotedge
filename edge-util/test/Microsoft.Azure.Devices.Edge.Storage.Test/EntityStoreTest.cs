// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    public class EntityStoreTest : EntityStoreTestBase
    {
        readonly IStoreProvider storeProvider;

        public EntityStoreTest()
        {
            this.storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
        }

        protected override IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName)
            => this.storeProvider.GetEntityStore<TK, TV>(entityName);

        protected override IEntityStore<TK, TV> GetEntityStore<TK, TV>(string backwardCompatibilityEntityName, string entityName)
            => this.storeProvider.GetEntityStore<TK, TV>(backwardCompatibilityEntityName, entityName);
    }
}
