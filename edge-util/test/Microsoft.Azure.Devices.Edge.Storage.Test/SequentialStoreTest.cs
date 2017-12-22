// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using Microsoft.Azure.Devices.Edge.Storage;

    public class SequentialStoreTest : SequentialStoreTestBase
    {
        protected override IEntityStore<byte[], TV> GetEntityStore<TV>(string entityName) => new EntityStore<byte[], TV>(new InMemoryDbStore(), entityName);
    }
}
