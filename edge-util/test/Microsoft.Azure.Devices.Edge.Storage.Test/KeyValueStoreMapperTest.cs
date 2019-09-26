// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class KeyValueStoreMapperTest
    {
        private const string DefaultDbName = "TestDb";

        public static IEnumerable<object[]> GetMappers()
        {
            var stringData = new Dictionary<string, string>();
            for (int i = 0; i < 10; i++)
            {
                stringData[$"key{i}"] = $"value{i}";
            }

            var objectData = new Dictionary<TestClass, TestClass>();
            for (int i = 0; i < 10; i++)
            {
                objectData[new TestClass($"key{i}", i)] = new TestClass($"value{i}", i);
            }

            var objectData2 = new Dictionary<string, TestClass>();
            for (int i = 0; i < 10; i++)
            {
                objectData2[$"key{i}"] = new TestClass($"value{i}", i);
            }

            yield return new object[]
            {
                new InMemoryDbStore(DefaultDbName),
                new BytesMapper<string>(),
                new BytesMapper<string>(),
                stringData
            };

            yield return new object[]
            {
                new InMemoryDbStore(DefaultDbName),
                new BytesMapper<TestClass>(),
                new BytesMapper<TestClass>(),
                objectData
            };

            yield return new object[]
            {
                new InMemoryDbStore(DefaultDbName),
                new BytesMapper<string>(),
                new BytesMapper<TestClass>(),
                objectData2
            };

            yield return new object[]
            {
                new KeyValueStoreMapper<string, byte[], string, byte[]>(new InMemoryDbStore(DefaultDbName), new BytesMapper<string>(), new BytesMapper<string>()),
                new JsonMapper<string>(),
                new JsonMapper<TestClass>(),
                objectData2
            };

            yield return new object[]
            {
                new KeyValueStoreMapper<string, byte[], string, byte[]>(new InMemoryDbStore(DefaultDbName), new BytesMapper<string>(), new BytesMapper<string>()),
                new JsonMapper<TestClass>(),
                new JsonMapper<TestClass>(),
                objectData
            };
        }

        [Theory]
        [MemberData(nameof(GetMappers))]
        public async Task BasicMapperTest<TK, TK1, TV, TV1>(
            IKeyValueStore<TK, TV> underlyingStore,
            ITypeMapper<TK1, TK> keyMapper,
            ITypeMapper<TV1, TV> valueMapper,
            IDictionary<TK1, TV1> items)
        {
            // Arrange
            var keyValueMapper = new KeyValueStoreMapper<TK1, TK, TV1, TV>(underlyingStore, keyMapper, valueMapper);

            // Act
            KeyValuePair<TK1, TV1> item = items.First();
            await keyValueMapper.Put(item.Key, item.Value);

            // Assert
            Option<TV1> value = await keyValueMapper.Get(item.Key);
            Assert.True(value.HasValue);
            Option<TV> underlyingValue = await underlyingStore.Get(keyMapper.From(item.Key));
            Assert.True(underlyingValue.HasValue);
            Assert.Equal(underlyingValue.OrDefault(), valueMapper.From(value.OrDefault()));
        }

        class TestClass : IEquatable<TestClass>
        {
            public TestClass(string prop1, int prop2)
            {
                this.Prop1 = prop1;
                this.Prop2 = prop2;
            }

            [JsonProperty("prop1")]
            public string Prop1 { get; }

            [JsonProperty("prop2")]
            public int Prop2 { get; }

            public override bool Equals(object obj)
                => this.Equals(obj as TestClass);

            public bool Equals(TestClass other)
                => other != null &&
                   this.Prop1 == other.Prop1 &&
                   this.Prop2 == other.Prop2;

            public override int GetHashCode()
                => HashCode.Combine(this.Prop1, this.Prop2);
        }
    }
}
