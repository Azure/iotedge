// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class EntityStoreTest : IClassFixture<TestRocksDbStoreProvider>
    {
        private readonly TestRocksDbStoreProvider rocksDbStoreProvider;

        public EntityStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.rocksDbStoreProvider = rocksDbStoreProvider;
        }

        [Fact]
        public async Task BasicTest()
        {
            string entityName = "basicTestEntity";
            var entityStore = new EntityStore<Key, Value>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            await entityStore.Put(new Key { Type = "A", Id = 1 }, new Value { Prop1 = "Foo", Prop2 = 10 });
            await entityStore.Put(new Key { Type = "A", Id = 2 }, new Value { Prop1 = "Foo", Prop2 = 20 });
            await entityStore.Put(new Key { Type = "A", Id = 3 }, new Value { Prop1 = "Foo", Prop2 = 30 });
            await entityStore.Put(new Key { Type = "B", Id = 1 }, new Value { Prop1 = "Bar", Prop2 = 10 });
            await entityStore.Put(new Key { Type = "B", Id = 2 }, new Value { Prop1 = "Bar", Prop2 = 20 });
            await entityStore.Put(new Key { Type = "B", Id = 3 }, new Value { Prop1 = "Bar", Prop2 = 30 });

            Option<Value> optionValue1 = await entityStore.Get(new Key { Type = "A", Id = 1 });
            Assert.True(optionValue1.HasValue);
            Value value1 = optionValue1.OrDefault();
            Assert.Equal("Foo", value1.Prop1);
            Assert.Equal(10, value1.Prop2);

            Option<Value> optionValue2 = await entityStore.Get(new Key { Type = "B", Id = 3 });
            Assert.True(optionValue2.HasValue);
            Value value2 = optionValue2.OrDefault();
            Assert.Equal("Bar", value2.Prop1);
            Assert.Equal(30, value2.Prop2);

            Assert.True(await entityStore.Contains(new Key { Type = "B", Id = 2 }));
            Assert.False(await entityStore.Contains(new Key { Type = "B", Id = 4 }));

            await entityStore.Remove(new Key { Type = "A", Id = 1 });
            Assert.False(await entityStore.Contains(new Key { Type = "A", Id = 1 }));
            Assert.False((await entityStore.Get(new Key { Type = "A", Id = 1 })).HasValue);
        }        

        [Fact]
        public async Task GetFirstLastDefaultTest()
        {
            string entityName = "firstLastTestEntity";
            var entityStore = new EntityStore<Key, Value>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            Option<(Key, Value)> firstEntryOption = await entityStore.GetFirstEntry();
            Assert.False(firstEntryOption.HasValue);

            Option<(Key, Value)> lastEntryOption = await entityStore.GetLastEntry();
            Assert.False(lastEntryOption.HasValue);
        }

        [Fact]
        public async Task PutOrUpdateTest()
        {
            string entityName = "purUpdateTestEntity";
            var entityStore = new EntityStore<string, string>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            await entityStore.PutOrUpdate("key1", "putValue", (v) => "updateValue");
            Option<string> value = await entityStore.Get("key1");
            Assert.Equal("putValue", value.OrDefault());
            await entityStore.PutOrUpdate("key1", "putValue", (v) => "updateValue");
            value = await entityStore.Get("key1");
            Assert.Equal("updateValue", value.OrDefault());
        }

        [Fact]
        public async Task UpdateTest()
        {
            string entityName = "updateTestEntity";
            var entityStore = new EntityStore<string, string>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            await entityStore.Put("key1", "putValue");
            Option<string> value = await entityStore.Get("key1");
            Assert.Equal("putValue", value.OrDefault());
            await entityStore.Update("key1", (v) => "updateValue");
            value = await entityStore.Get("key1");
            Assert.Equal("updateValue", value.OrDefault());
        }

        class Key
        {
            public string Type { get; set; }
            public int Id { get; set; }
        }

        class Value
        {
            public string Prop1 { get; set; }
            public int Prop2 { get; set; }
        }
    }
}
