// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class EntityStoreTest
    {
        [Fact]
        public async Task BasicTest()
        {
            var entityStore = new EntityStore<Key, Value>(new InMemoryDbStore(), "testEntity");
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
        }

        [Fact]
        public async Task GetFirstLastDefaultTest()
        {
            var entityStore = new EntityStore<Key, Value>(new InMemoryDbStore(), "testEntity");
            Option<(Key, Value)> firstEntryOption = await entityStore.GetFirstEntry();
            Assert.False(firstEntryOption.HasValue);

            Option<(Key, Value)> lastEntryOption = await entityStore.GetLastEntry();
            Assert.False(lastEntryOption.HasValue);
        }

        [Fact]
        public async Task PutOrUpdateTest()
        {
            var entityStore = new EntityStore<string, string>(new InMemoryDbStore(), "testEntity");
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
            var entityStore = new EntityStore<string, string>(new InMemoryDbStore(), "testEntity");
            await entityStore.Put("key1", "putValue");
            Option<string> value = await entityStore.Get("key1");
            Assert.Equal("putValue", value.OrDefault());
            await entityStore.Update("key1", (v) => "updateValue");
            value = await entityStore.Get("key1");
            Assert.Equal("updateValue", value.OrDefault());
        }

        [Fact]
        public async Task IterateTest()
        {
            var entityStore = new EntityStore<int, string>(new InMemoryDbStore(), "testEntity");
            for (int i = 0; i < 100; i++)
            {
                await entityStore.Put(i, $"{i}");
            }

            var keys = new List<int>();
            await entityStore.IterateBatch(100, (key, value) =>
            {
                keys.Add(key);
                return Task.CompletedTask;
            });

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(i, keys[i]);
            }

            keys = new List<int>();
            await entityStore.IterateBatch(20, 20, (key, value) =>
            {
                keys.Add(key);
                return Task.CompletedTask;
            });

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(i + 20, keys[i]);
            }
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
