// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ModelIdStoreTest
    {
        [Fact]
        public async Task SmokeTest()
        {
            // Arrange
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("modelId");
            var modelIdStore = new ModelIdStore(store);

            var deviceToModelIds = new Dictionary<string, string>
            {
                ["d1"] = "dtmi:example:capabailityModels:MXChip;1",
                ["d2"] = "dtmi:example2:capabailityModels:MXChip;1",
                ["d3"] = "dtmi:example3:capabailityModels:MXChip;1",
                ["d3/m1"] = "dtmi:example4:capabailityModels:MXChip;1",
                ["d3/m2"] = "dtmi:example5:capabailityModels:MXChip;1"
            };

            // Act
            foreach (KeyValuePair<string, string> kvp in deviceToModelIds)
            {
                await modelIdStore.SetModelId(kvp.Key, kvp.Value);
            }

            var receivedDeviceModelIds = new Dictionary<string, string>();

            foreach (string id in deviceToModelIds.Keys)
            {
                Option<string> modelId = await modelIdStore.GetModelId(id);
                modelId.ForEach(m => receivedDeviceModelIds.Add(id, m));
            }

            // Assert
            Assert.Equal(deviceToModelIds.Count, receivedDeviceModelIds.Count);

            foreach (KeyValuePair<string, string> kvp in deviceToModelIds)
            {
                Assert.Equal(kvp.Value, receivedDeviceModelIds[kvp.Key]);
            }
        }

        [Fact]
        public async Task EmptyModelIdTest()
        {
            // Arrange
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("modelId");
            var modelIdStore = new ModelIdStore(store);

            // Act
            await modelIdStore.SetModelId("d1", string.Empty);
            Option<string> modelId = await modelIdStore.GetModelId("d1");

            // Assert
            Assert.False(modelId.HasValue);
        }

        [Fact]
        public async Task WhitespaceModelIdTest()
        {
            // Arrange
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("modelId");
            var modelIdStore = new ModelIdStore(store);

            // Act
            await modelIdStore.SetModelId("d1", "            ");
            Option<string> modelId = await modelIdStore.GetModelId("d1");

            // Assert
            Assert.False(modelId.HasValue);
        }

        [Fact]
        public void ModelIdCtorTest()
        {
            new ModelIdStore(Mock.Of<IKeyValueStore<string, string>>());

            Assert.Throws<ArgumentNullException>(() => new ModelIdStore(null));
        }
    }
}
