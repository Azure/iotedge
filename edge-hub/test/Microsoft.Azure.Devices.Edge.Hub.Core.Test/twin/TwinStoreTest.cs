// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.twin
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
                new TwinStoreEntity()
            );

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

            //Assert
            Assert.True(t1.HasValue);
            Assert.Equal(t1.OrDefault().Properties.Desired.ToJson(), "{\"dp1\":\"d1\"}");
            Assert.Equal(t1.OrDefault().Properties.Reported.ToJson(), "{\"rp1\":\"r1\"}");

            // Act
            Option<Twin> t2 = await twinStore.Get("d2");

            //Assert
            Assert.False(t2.HasValue);

            // Act
            Option<Twin> t3 = await twinStore.Get("d3");

            //Assert
            Assert.False(t3.HasValue);

            // Act
            Option<Twin> t4 = await twinStore.Get("d4");

            //Assert
            Assert.True(t4.HasValue);
            Assert.Equal(t4.OrDefault().Properties.Desired.ToJson(), "{\"dp4\":\"d4\"}");
            Assert.Equal(t4.OrDefault().Properties.Reported.ToJson(), "{\"rp4\":\"r4\"}");
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
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"v1\",\"p2\":\"v2\"}");

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}");

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":{\"p31\":\"v31\"}}");

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"vp1\",\"p2\":\"vp2\",\"p3\":{\"p31\":\"v32\"}}");
        }

        [Fact]
        public async Task UpdatedDesiredPropertiesTest()
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
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"v1\",\"p2\":\"v2\"}");

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch1);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":\"v3\"}");

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch2);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"vp1\",\"p2\":\"v2\",\"p3\":{\"p31\":\"v31\"}}");

            // Act
            await twinStore.UpdateReportedProperties(id, rpatch3);

            // Assert
            twin = await twinStore.Get(id);
            Assert.True(twin.HasValue);
            Assert.Equal(twin.OrDefault().Properties.Reported.ToJson(), "{\"p1\":\"vp1\",\"p2\":\"vp2\",\"p3\":{\"p31\":\"v32\"}}");
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
