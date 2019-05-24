// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ProductInfoStoreTest
    {
        [Fact]
        public async Task SmokeTest()
        {
            // Arrange
            string edgeProductInfo = "IoTEdge 1.0.7";
            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            IEntityStore<string, string> store = storeProvider.GetEntityStore<string, string>("productInfo");
            var productInfoStore = new ProductInfoStore(store, edgeProductInfo);

            var deviceProductInfos = new Dictionary<string, string>
            {
                ["d1"] = Guid.NewGuid().ToString(),
                ["d2"] = Guid.NewGuid().ToString(),
                ["d3"] = Guid.NewGuid().ToString(),
                ["d3/m1"] = Guid.NewGuid().ToString(),
                ["d3/m2"] = Guid.NewGuid().ToString()
            };

            // Act
            foreach (KeyValuePair<string, string> kvp in deviceProductInfos)
            {
                await productInfoStore.SetProductInfo(kvp.Key, kvp.Value);
            }

            var receivedDeviceInfos = new Dictionary<string, string>();
            var receivedEdgeDeviceInfos = new Dictionary<string, string>();

            foreach (string id in deviceProductInfos.Keys)
            {
                string productInfo = await productInfoStore.GetProductInfo(id);
                string deviceEdgeProductInfo = await productInfoStore.GetEdgeProductInfo(id);
                receivedDeviceInfos.Add(id, productInfo);
                receivedEdgeDeviceInfos.Add(id, deviceEdgeProductInfo);
            }

            // Assert
            Assert.Equal(deviceProductInfos.Count, receivedDeviceInfos.Count);
            Assert.Equal(deviceProductInfos.Count, receivedEdgeDeviceInfos.Count);

            foreach (KeyValuePair<string, string> kvp in deviceProductInfos)
            {
                Assert.Equal(kvp.Value, receivedDeviceInfos[kvp.Key]);
                Assert.Equal($"{kvp.Value} {edgeProductInfo}", receivedEdgeDeviceInfos[kvp.Key]);
            }
        }

        [Fact]
        public void ProductInfoCtorTest()
        {
            new ProductInfoStore(Mock.Of<IKeyValueStore<string, string>>(), "Foo bar");

            new ProductInfoStore(Mock.Of<IKeyValueStore<string, string>>(), string.Empty);

            Assert.Throws<ArgumentNullException>(() => new ProductInfoStore(Mock.Of<IKeyValueStore<string, string>>(), null));

            Assert.Throws<ArgumentNullException>(() => new ProductInfoStore(null, string.Empty));
        }
    }
}
