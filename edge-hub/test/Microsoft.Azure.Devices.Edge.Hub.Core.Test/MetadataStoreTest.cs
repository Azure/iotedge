// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class MetadataStoreTest
    {
        readonly string dummyProductInfo = "testProductInfo";

        [Fact]
        public async Task SmokeTest()
        {
            // Arrange
            string edgeProductInfo = "IoTEdge 1.0.7";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("connectionMetadata");
            var metadataStore = new MetadataStore(store, edgeProductInfo);

            var deviceProductInfos = new Dictionary<string, string>
            {
                ["d1"] = Guid.NewGuid().ToString(),
                ["d2"] = Guid.NewGuid().ToString(),
                ["d3"] = Guid.NewGuid().ToString(),
                ["d3/m1"] = Guid.NewGuid().ToString(),
                ["d3/m2"] = Guid.NewGuid().ToString()
            };

            var deviceToModelIds = new Dictionary<string, string>
            {
                ["d1"] = "dtmi:example:capabailityModels:MXChip;1",
                ["d2"] = "dtmi:example2:capabailityModels:MXChip;1",
                ["d3"] = "dtmi:example3:capabailityModels:MXChip;1",
                ["d3/m1"] = "dtmi:example4:capabailityModels:MXChip;1",
                ["d3/m2"] = "dtmi:example5:capabailityModels:MXChip;1"
            };

            // Act
            foreach (KeyValuePair<string, string> kvp in deviceProductInfos)
            {
                await metadataStore.SetProductInfo(kvp.Key, kvp.Value);
            }

            foreach (KeyValuePair<string, string> kvp in deviceToModelIds)
            {
                await metadataStore.SetModelId(kvp.Key, kvp.Value);
            }

            var receivedDeviceInfos = new Dictionary<string, string>();
            var receivedEdgeDeviceInfos = new Dictionary<string, string>();
            var receivedDeviceModelIds = new Dictionary<string, string>();

            foreach (string id in deviceProductInfos.Keys)
            {
                ConnectionMetadata connectionMetadata = await metadataStore.GetMetadata(id);
                string productInfo = connectionMetadata.ProductInfo;
                string deviceEdgeProductInfo = connectionMetadata.EdgeProductInfo;

                receivedDeviceInfos.Add(id, productInfo);
                receivedEdgeDeviceInfos.Add(id, deviceEdgeProductInfo);
            }

            foreach (string id in deviceToModelIds.Keys)
            {
                ConnectionMetadata connectionMetadata = await metadataStore.GetMetadata(id);
                Option<string> modelId = connectionMetadata.ModelId;
                modelId.ForEach(m => receivedDeviceModelIds.Add(id, m));
            }

            // Assert
            Assert.Equal(deviceProductInfos.Count, receivedDeviceInfos.Count);
            Assert.Equal(deviceProductInfos.Count, receivedEdgeDeviceInfos.Count);
            Assert.Equal(deviceToModelIds.Count, receivedDeviceModelIds.Count);

            foreach (KeyValuePair<string, string> kvp in deviceProductInfos)
            {
                Assert.Equal(kvp.Value, receivedDeviceInfos[kvp.Key]);
                Assert.Equal($"{kvp.Value} {edgeProductInfo}", receivedEdgeDeviceInfos[kvp.Key]);
            }

            foreach (KeyValuePair<string, string> kvp in deviceToModelIds)
            {
                Assert.Equal(kvp.Value, receivedDeviceModelIds[kvp.Key]);
            }
        }

        [Fact]
        public async Task SetMetadataSmokeTest()
        {
            // Arrange
            string edgeProductInfo = "IoTEdge 1.0.7";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("connectionMetadata");
            var metadataStore = new MetadataStore(store, edgeProductInfo);
            string id = "testId";
            string productInfo = "testProductInfo";
            string productInfo2 = "testProductInfo2";
            string modelId = "testModelId";
            string modelId2 = "testModelId";

            // Act
            await metadataStore.SetMetadata(id, productInfo, Option.Some(modelId));
            ConnectionMetadata connectionMetadata = await metadataStore.GetMetadata(id);

            // Assert
            Assert.True(connectionMetadata.ModelId.HasValue);
            connectionMetadata.ModelId.ForEach(m => Assert.Equal(modelId, m));
            Assert.Equal(productInfo, connectionMetadata.ProductInfo);

            // Act
            await metadataStore.SetModelId(id, modelId2);
            connectionMetadata = await metadataStore.GetMetadata(id);

            // Assert
            Assert.True(connectionMetadata.ModelId.HasValue);
            connectionMetadata.ModelId.ForEach(m => Assert.Equal(modelId2, m));

            // Act
            await metadataStore.SetProductInfo(id, productInfo2);
            connectionMetadata = await metadataStore.GetMetadata(id);

            // Assert
            Assert.Equal(productInfo2, connectionMetadata.ProductInfo);
        }

        [Fact]
        public async Task EmptyProductInfoTest()
        {
            // Arrange
            string edgeProductInfo = "IoTEdge 1.0.7";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("connectionMetadata");
            var metadataStore = new MetadataStore(store, edgeProductInfo);
            string id = "id";

            // Act
            await metadataStore.SetProductInfo(id, string.Empty);
            string productInfoValue = (await metadataStore.GetMetadata(id)).ProductInfo;

            // Assert
            Assert.Equal(string.Empty, productInfoValue);
        }

        [Fact]
        public async Task NoProductInfoTest()
        {
            // Arrange
            string edgeProductInfo = "IoTEdge 1.0.7";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("connectionMetadata");
            var metadataStore = new MetadataStore(store, edgeProductInfo);

            // Act
            string productInfoValue = (await metadataStore.GetMetadata("id")).ProductInfo;

            // Assert
            Assert.Equal(string.Empty, productInfoValue);
        }

        [Fact]
        public async Task EmptyModelIdTest()
        {
            // Arrange
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("connectionMetadata");
            var metadataStore = new MetadataStore(store, this.dummyProductInfo);

            // Act
            await metadataStore.SetModelId("id", string.Empty);
            Option<string> modelId = (await metadataStore.GetMetadata("id")).ModelId;

            // Assert
            Assert.False(modelId.HasValue);
        }

        [Fact]
        public async Task ModelIdMigrationTest()
        {
            string testProductInfo = "testProductInfo";
            string id = "id";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            Mock<IEntityStore<string, string>> store = new Mock<IEntityStore<string, string>>();
            store.Setup(m => m.Get(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(testProductInfo)));
            var metadataStore = new MetadataStore(store.Object, this.dummyProductInfo);
            ConnectionMetadata connectionMetadata = await metadataStore.GetMetadata(id);

            Assert.Equal(testProductInfo, connectionMetadata.ProductInfo);
            Assert.False(connectionMetadata.ModelId.HasValue);
        }

        [Fact]
        public async Task ProductInfoMigrationTest()
        {
            string testProductInfo = "testProductInfo";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            Mock<IEntityStore<string, string>> store = new Mock<IEntityStore<string, string>>();
            store.Setup(m => m.Get(It.IsAny<string>())).Returns(Task.FromResult(Option.Some(testProductInfo)));
            var metadataStore = new MetadataStore(store.Object, this.dummyProductInfo);
            string productInfo = (await metadataStore.GetMetadata("id")).ProductInfo;
            Assert.Equal(testProductInfo, productInfo);
        }

        [Fact]
        public async Task WhitespaceModelIdTest()
        {
            // Arrange
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("connectionMetadata");
            var metadataStore = new MetadataStore(store, this.dummyProductInfo);

            // Act
            await metadataStore.SetModelId("id", "            ");
            Option<string> modelId = (await metadataStore.GetMetadata("id")).ModelId;

            // Assert
            Assert.False(modelId.HasValue);
        }

        [Fact]
        public void MetadataStoreCtorTest()
        {
            new MetadataStore(Mock.Of<IKeyValueStore<string, string>>(), "Foo bar");

            new MetadataStore(Mock.Of<IKeyValueStore<string, string>>(), string.Empty);

            Assert.Throws<ArgumentNullException>(() => new MetadataStore(Mock.Of<IKeyValueStore<string, string>>(), null));

            Assert.Throws<ArgumentNullException>(() => new MetadataStore(null, string.Empty));
        }
    }
}
