// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Client;
    using Xunit;

    [Unit]
    public class TwinStoreTest
    {
        [Fact]
        public async Task GetTwinTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            await twinEntityStore.Put(
                "d1",
                new TwinStoreEntity(
                    new TwinProperties
                    {
                        Desired = new PropertyCollection
                        {
                            ["dp1"] = "d1"
                        },
                        Reported = new PropertyCollection
                        {
                            ["rp1"] = "r1"
                        }
                    }));

            await twinEntityStore.Put(
                "d2",
                new TwinStoreEntity(
                    new PropertyCollection
                    {
                        ["rp2"] = "d2"
                    }));

            await twinEntityStore.Put(
                "d3",
                new TwinStoreEntity());

            await twinEntityStore.Put(
                "d4",
                new TwinStoreEntity(
                    new TwinProperties
                    {
                        Desired = new PropertyCollection
                        {
                            ["dp4"] = "d4"
                        },
                        Reported = new PropertyCollection
                        {
                            ["rp4"] = "r4"
                        }
                    },
                    new PropertyCollection
                    {
                        ["rp4"] = "r4"
                    }));
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            // Act
            Option<TwinProperties> t1 = await twinStore.Get("d1");

            // Assert
            Assert.True(t1.HasValue);
            Assert.Equal("{\"dp1\":\"d1\"}", t1.OrDefault().Desired.GetSerializedString());
            Assert.Equal("{\"rp1\":\"r1\"}", t1.OrDefault().Reported.GetSerializedString());

            // Act
            Option<TwinProperties> t2 = await twinStore.Get("d2");

            // Assert
            Assert.False(t2.HasValue);

            // Act
            Option<TwinProperties> t3 = await twinStore.Get("d3");

            // Assert
            Assert.False(t3.HasValue);

            // Act
            Option<TwinProperties> t4 = await twinStore.Get("d4");

            // Assert
            Assert.True(t4.HasValue);
            Assert.Equal("{\"dp4\":\"d4\"}", t4.OrDefault().Desired.GetSerializedString());
            Assert.Equal("{\"rp4\":\"r4\"}", t4.OrDefault().Reported.GetSerializedString());
        }

        [Fact]
        public async Task UpdatedReportedPropertiesTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            var rbase = new PropertyCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2"
            };

            var rpatch1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3"
            };

            var rpatch2 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = new
                {
                    p31 = "v31"
                }
            };

            var rpatch3 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["p3"] = new
                {
                    p31 = "v32"
                }
            };

            string id = "d1";

            // Act
            await twinStore.UpdateReportedProperties(id, rbase);

            // Assert
            Option<TwinProperties> twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\"}", twin.OrDefault().Reported.GetSerializedString());

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}", twin.OrDefault().Reported.GetSerializedString());

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":{\"p31\":\"v31\"}}", twin.OrDefault().Reported.GetSerializedString());

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp2\",\"p3\":{\"p31\":\"v32\"}}", twin.OrDefault().Reported.GetSerializedString());
        }

        [Fact]
        public async Task UpdatedDesiredPropertiesTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            var dbase = new PropertyCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2",
                ["$version"] = 0
            };

            var basetwin = new TwinProperties
            {
                Desired = dbase
            };

            var dpatch1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var dpatch2 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = new
                {
                    p31 = "v31"
                },
                ["$version"] = 2
            };

            var dpatch3 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["p3"] = new
                {
                    p31 = "v32"
                },
                ["$version"] = 3
            };

            var dpatch4 = new PropertyCollection
            {
                ["p2"] = "vp4",
                ["p3"] = new
                {
                    p31 = "v50"
                },
                ["$version"] = 3
            };

            string id = "d1";

            // Act
            await twinStore.Update(id, basetwin);

            // Assert
            Option<TwinProperties> twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\",\"$version\":0}", twin.OrDefault().Desired.GetSerializedString());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"$version\":1,\"p3\":\"v3\"}", twin.OrDefault().Desired.GetSerializedString());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"$version\":2,\"p3\":{\"p31\":\"v31\"}}", twin.OrDefault().Desired.GetSerializedString());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp2\",\"$version\":3,\"p3\":{\"p31\":\"v32\"}}", twin.OrDefault().Desired.GetSerializedString());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch4);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp2\",\"$version\":3,\"p3\":{\"p31\":\"v32\"}}", twin.OrDefault().Desired.GetSerializedString());
        }

        [Fact]
        public async Task UpdatedTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            var dbase = new PropertyCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2",
                ["$version"] = 0
            };

            var rbase = new PropertyCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2",
                ["$version"] = 0
            };

            var basetwin = new TwinProperties
            {
                Desired = dbase,
                Reported = rbase
            };

            var desired1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var reported1 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var twin1 = new TwinProperties
            {
                Desired = desired1,
                Reported = reported1
            };

            var desired2 = new PropertyCollection
            {
                ["p2"] = "vp2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "vp3",
                ["$version"] = 2
            };

            var twin2 = new TwinProperties
            {
                Desired = desired2,
                Reported = reported2
            };

            var desired3 = new PropertyCollection
            {
                ["p2"] = "v10",
                ["p3"] = "vp3",
                ["$version"] = 3
            };

            var reported3 = new PropertyCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v10",
                ["$version"] = 3
            };

            var twin3 = new TwinProperties
            {
                Desired = desired3,
                Reported = reported3
            };

            string id = "d1";

            // Act
            await twinStore.Update(id, basetwin);

            // Assert
            Option<TwinProperties> twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\",\"$version\":0}", twin.OrDefault().Desired.GetSerializedString());
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\",\"$version\":0}", twin.OrDefault().Reported.GetSerializedString());

            // Act
            await twinStore.Update(id, twin1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p3\":\"v3\",\"$version\":1}", twin.OrDefault().Desired.GetSerializedString());
            Assert.Equal("{\"p1\":\"vp1\",\"p3\":\"v3\",\"$version\":1}", twin.OrDefault().Reported.GetSerializedString());

            // Act
            await twinStore.Update(id, twin2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p2\":\"vp2\",\"p3\":\"v3\",\"$version\":2}", twin.OrDefault().Desired.GetSerializedString());
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp3\",\"$version\":2}", twin.OrDefault().Reported.GetSerializedString());

            // Act
            await twinStore.Update(id, twin3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p2\":\"v10\",\"p3\":\"vp3\",\"$version\":3}", twin.OrDefault().Desired.GetSerializedString());
            Assert.Equal("{\"p1\":\"vp1\",\"p3\":\"v10\",\"$version\":3}", twin.OrDefault().Reported.GetSerializedString());
        }

        static IEntityStore<string, TwinStoreEntity> GetTwinEntityStore()
        {
            var dbStoreProvider = new InMemoryDbStoreProvider();
            var entityStoreProvider = new StoreProvider(dbStoreProvider);
            IEntityStore<string, TwinStoreEntity> entityStore = entityStoreProvider.GetEntityStore<string, TwinStoreEntity>($"twin{Guid.NewGuid()}");
            return entityStore;
        }
    }
}
