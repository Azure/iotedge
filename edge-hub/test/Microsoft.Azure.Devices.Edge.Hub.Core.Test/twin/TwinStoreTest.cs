// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
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
                    new Twin
                    {
                        Properties = new TwinProperties
                        {
                            Desired = new TwinCollection
                            {
                                ["dp1"] = "d1"
                            },
                            Reported = new TwinCollection
                            {
                                ["rp1"] = "r1"
                            }
                        }
                    }));

            await twinEntityStore.Put(
                "d2",
                new TwinStoreEntity(
                    new TwinCollection
                    {
                        ["rp2"] = "d2"
                    }));

            await twinEntityStore.Put(
                "d3",
                new TwinStoreEntity());

            await twinEntityStore.Put(
                "d4",
                new TwinStoreEntity(
                    new Twin
                    {
                        Properties = new TwinProperties
                        {
                            Desired = new TwinCollection
                            {
                                ["dp4"] = "d4"
                            },
                            Reported = new TwinCollection
                            {
                                ["rp4"] = "r4"
                            }
                        }
                    },
                    new TwinCollection
                    {
                        ["rp4"] = "r4"
                    }));
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            // Act
            Option<Twin> t1 = await twinStore.Get("d1");

            // Assert
            Assert.True(t1.HasValue);
            Assert.Equal("{\"dp1\":\"d1\"}", t1.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"rp1\":\"r1\"}", t1.OrDefault().Properties.Reported.ToJson());

            // Act
            Option<Twin> t2 = await twinStore.Get("d2");

            // Assert
            Assert.False(t2.HasValue);

            // Act
            Option<Twin> t3 = await twinStore.Get("d3");

            // Assert
            Assert.False(t3.HasValue);

            // Act
            Option<Twin> t4 = await twinStore.Get("d4");

            // Assert
            Assert.True(t4.HasValue);
            Assert.Equal("{\"dp4\":\"d4\"}", t4.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"rp4\":\"r4\"}", t4.OrDefault().Properties.Reported.ToJson());
        }

        [Fact]
        public async Task UpdatedReportedPropertiesTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            var rbase = new TwinCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2"
            };

            var rpatch1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3"
            };

            var rpatch2 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = new
                {
                    p31 = "v31"
                }
            };

            var rpatch3 = new TwinCollection
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
            Option<Twin> twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\"}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":{\"p31\":\"v31\"}}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp2\",\"p3\":{\"p31\":\"v32\"}}", twin.OrDefault().Properties.Reported.ToJson());
        }

        [Fact]
        public async Task UpdatedDesiredPropertiesTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            var dbase = new TwinCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2",
                ["$version"] = 0
            };

            var basetwin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = dbase
                }
            };

            var dpatch1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var dpatch2 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = new
                {
                    p31 = "v31"
                },
                ["$version"] = 2
            };

            var dpatch3 = new TwinCollection
            {
                ["p2"] = "vp2",
                ["p3"] = new
                {
                    p31 = "v32"
                },
                ["$version"] = 3
            };

            var dpatch4 = new TwinCollection
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
            Option<Twin> twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\",\"$version\":0}", twin.OrDefault().Properties.Desired.ToJson());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"$version\":1,\"p3\":\"v3\"}", twin.OrDefault().Properties.Desired.ToJson());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"v2\",\"$version\":2,\"p3\":{\"p31\":\"v31\"}}", twin.OrDefault().Properties.Desired.ToJson());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp2\",\"$version\":3,\"p3\":{\"p31\":\"v32\"}}", twin.OrDefault().Properties.Desired.ToJson());

            // Act
            await twinStore.UpdateDesiredProperties(id, dpatch4);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp2\",\"$version\":3,\"p3\":{\"p31\":\"v32\"}}", twin.OrDefault().Properties.Desired.ToJson());
        }

        [Fact]
        public async Task UpdatedTest()
        {
            // Arrange
            IEntityStore<string, TwinStoreEntity> twinEntityStore = GetTwinEntityStore();
            ITwinStore twinStore = new TwinStore(twinEntityStore);

            var dbase = new TwinCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2",
                ["$version"] = 0
            };

            var rbase = new TwinCollection
            {
                ["p1"] = "v1",
                ["p2"] = "v2",
                ["$version"] = 0
            };

            var basetwin = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = dbase,
                    Reported = rbase
                }
            };

            var desired1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var reported1 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v3",
                ["$version"] = 1
            };

            var twin1 = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = desired1,
                    Reported = reported1
                }
            };

            var desired2 = new TwinCollection
            {
                ["p2"] = "vp2",
                ["p3"] = "v3",
                ["$version"] = 2
            };

            var reported2 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p2"] = "vp3",
                ["$version"] = 2
            };

            var twin2 = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = desired2,
                    Reported = reported2
                }
            };

            var desired3 = new TwinCollection
            {
                ["p2"] = "v10",
                ["p3"] = "vp3",
                ["$version"] = 3
            };

            var reported3 = new TwinCollection
            {
                ["p1"] = "vp1",
                ["p3"] = "v10",
                ["$version"] = 3
            };

            var twin3 = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = desired3,
                    Reported = reported1
                }
            };

            var twin4 = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = desired2,
                    Reported = reported3
                }
            };

            var twin5 = new Twin
            {
                Properties = new TwinProperties
                {
                    Desired = desired2,
                    Reported = reported2
                }
            };

            string id = "d1";

            // Act
            await twinStore.Update(id, basetwin);

            // Assert
            Option<Twin> twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\",\"$version\":0}", twin.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"p1\":\"v1\",\"p2\":\"v2\",\"$version\":0}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.Update(id, twin1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p1\":\"vp1\",\"p3\":\"v3\",\"$version\":1}", twin.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"p1\":\"vp1\",\"p3\":\"v3\",\"$version\":1}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.Update(id, twin2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p2\":\"vp2\",\"p3\":\"v3\",\"$version\":2}", twin.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp3\",\"$version\":2}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.Update(id, twin3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p2\":\"vp2\",\"p3\":\"v3\",\"$version\":2}", twin.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp3\",\"$version\":2}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.Update(id, twin4);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p2\":\"vp2\",\"p3\":\"v3\",\"$version\":2}", twin.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp3\",\"$version\":2}", twin.OrDefault().Properties.Reported.ToJson());

            // Act
            await twinStore.Update(id, twin5);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal("{\"p2\":\"vp2\",\"p3\":\"v3\",\"$version\":2}", twin.OrDefault().Properties.Desired.ToJson());
            Assert.Equal("{\"p1\":\"vp1\",\"p2\":\"vp3\",\"$version\":2}", twin.OrDefault().Properties.Reported.ToJson());
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
