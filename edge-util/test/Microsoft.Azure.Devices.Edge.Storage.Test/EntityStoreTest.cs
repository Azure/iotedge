// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using Microsoft.Azure.Devices.Edge.Storage;

    public class EntityStoreTest : EntityStoreTestBase
    {
        protected override IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName) => new EntityStore<TK, TV>(new InMemoryDbStore(), entityName);
    }
}
