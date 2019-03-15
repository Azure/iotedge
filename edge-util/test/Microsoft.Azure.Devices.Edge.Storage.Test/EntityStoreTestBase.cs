// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    [Integration]
    public abstract class EntityStoreTestBase
    {
        [Fact]
        [TestPriority(301)]
        public async Task BasicTest()
        {
            IEntityStore<Key, Value> entityStore = this.GetEntityStore<Key, Value>("basicTestEntity");
            await entityStore.Put(new Key { Type = "A", Id = 1 }, new Value { Prop1 = "Foo", Prop2 = 10 });
            await entityStore.Put(new Key { Type = "A", Id = 2 }, new Value { Prop1 = "Foo", Prop2 = 20 });
            await entityStore.Put(new Key { Type = "A", Id = 3 }, new Value { Prop1 = "Foo", Prop2 = 30 });
            await entityStore.Put(new Key { Type = "B", Id = 1 }, new Value { Prop1 = "Bar", Prop2 = 10 });
            await entityStore.Put(new Key { Type = "B", Id = 2 }, new Value { Prop1 = "Bar", Prop2 = 20 });
            await entityStore.Put(new Key { Type = "B", Id = 3 }, new Value { Prop1 = "Bar", Prop2 = 30 });

            // act/assert to validate key.
            Option<(Key key, Value value)> itemRetrieved = await entityStore.GetFirstEntry();

            Assert.True(itemRetrieved.HasValue);
            itemRetrieved.ForEach(
                pair =>
                {
                    Assert.Equal("A", pair.key.Type);
                    Assert.Equal(1, pair.key.Id);
                });

            itemRetrieved = await entityStore.GetLastEntry();
            Assert.True(itemRetrieved.HasValue);
            itemRetrieved.ForEach(
                pair =>
                {
                    Assert.Equal("B", pair.key.Type);
                    Assert.Equal(3, pair.key.Id);
                });

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
        [TestPriority(302)]
        public async Task GetFirstLastDefaultTest()
        {
            IEntityStore<Key, Value> entityStore = this.GetEntityStore<Key, Value>("firstLastTest");
            Option<(Key, Value)> firstEntryOption = await entityStore.GetFirstEntry();
            Assert.False(firstEntryOption.HasValue);

            Option<(Key, Value)> lastEntryOption = await entityStore.GetLastEntry();
            Assert.False(lastEntryOption.HasValue);
        }

        [Fact]
        [TestPriority(303)]
        public async Task PutOrUpdateTest()
        {
            IEntityStore<string, string> entityStore = this.GetEntityStore<string, string>("putOrUpdate");
            string value = await entityStore.PutOrUpdate("key1", "putValue", v => "updateValue");
            Assert.Equal("putValue", value);
            Option<string> getPutValue = await entityStore.Get("key1");
            Assert.Equal("putValue", getPutValue.OrDefault());
            value = await entityStore.PutOrUpdate("key1", "putValue", v => "updateValue");
            Assert.Equal("updateValue", value);
            Option<string> getValue = await entityStore.Get("key1");
            Assert.Equal("updateValue", getValue.OrDefault());
        }

        [Fact]
        [TestPriority(304)]
        public async Task FindOrPutTest()
        {
            IEntityStore<string, string> entityStore = this.GetEntityStore<string, string>("findOrPut");
            string value = await entityStore.FindOrPut("key1", "putValue");
            Assert.Equal("putValue", value);
            Option<string> getPutValue = await entityStore.Get("key1");
            Assert.Equal("putValue", getPutValue.OrDefault());
            value = await entityStore.FindOrPut("key1", "putValue2");
            Assert.Equal("putValue", value);
            Option<string> getFindValue = await entityStore.Get("key1");
            Assert.Equal("putValue", getFindValue.OrDefault());
        }

        [Fact]
        [TestPriority(305)]
        public async Task UpdateTest()
        {
            IEntityStore<string, string> entityStore = this.GetEntityStore<string, string>("updateTestEntity");
            await entityStore.Put("key1", "putValue");
            Option<string> value = await entityStore.Get("key1");
            Assert.Equal("putValue", value.OrDefault());
            string updatedValue = await entityStore.Update("key1", v => "updateValue");
            Assert.Equal("updateValue", updatedValue);
            Option<string> getUpdatedValue = await entityStore.Get("key1");
            Assert.Equal(getUpdatedValue.OrDefault(), updatedValue);
        }

        protected abstract IEntityStore<TK, TV> GetEntityStore<TK, TV>(string entityName);

        public class Key
        {
            public string Type { get; set; }

            public int Id { get; set; }
        }

        public class Value
        {
            public string Prop1 { get; set; }

            public int Prop2 { get; set; }
        }
    }
}
