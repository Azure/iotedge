// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class SingleOrArrayConverterTest
    {
        public static IEnumerable<object[]> GetRoundtripData()
        {
            yield return new object[]
            {
                new TestClass(
                    new List<string> { "foo" },
                    new List<PropertyClass> { new PropertyClass("Bar") }),
                "{\"items\":\"foo\",\"propertyItems\":{\"testProp\":\"Bar\"}}"
            };

            yield return new object[]
            {
                new TestClass(
                    new List<string> { "foo", "foo2" },
                    new List<PropertyClass> { new PropertyClass("Bar"), new PropertyClass("Bar2") }),
                "{\"items\":[\"foo\",\"foo2\"],\"propertyItems\":[{\"testProp\":\"Bar\"},{\"testProp\":\"Bar2\"}]}"
            };
        }

        [Theory]
        [MemberData(nameof(GetRoundtripData))]
        public void RoundtripTest(TestClass testObj, string expectedJson)
        {
            // Act
            string json = JsonConvert.SerializeObject(testObj);

            // Assert
            Assert.Equal(expectedJson, json);

            // Act
            TestClass deserializedObject = JsonConvert.DeserializeObject<TestClass>(json);

            // Assert
            Assert.Equal(testObj, deserializedObject);
        }

        public class PropertyClass : IEquatable<PropertyClass>
        {
            public PropertyClass(string testProp)
            {
                this.TestProp = testProp;
            }

            [JsonProperty("testProp")]
            public string TestProp { get; }

            public override bool Equals(object obj) => this.Equals(obj as PropertyClass);

            public bool Equals(PropertyClass other) => other != null &&
                                                       this.TestProp == other.TestProp;

            public override int GetHashCode() => HashCode.Combine(this.TestProp);
        }

        public class TestClass : IEquatable<TestClass>
        {
            public TestClass(List<string> items, List<PropertyClass> propertyItems)
            {
                this.Items = items;
                this.PropertyItems = propertyItems;
            }

            [JsonProperty("items")]
            [JsonConverter(typeof(SingleOrArrayConverter<string>))]
            public List<string> Items { get; }

            [JsonProperty("propertyItems")]
            [JsonConverter(typeof(SingleOrArrayConverter<PropertyClass>))]
            public List<PropertyClass> PropertyItems { get; }

            public override bool Equals(object obj)
                => this.Equals(obj as TestClass);

            public bool Equals(TestClass other)
                => other != null &&
                   !this.Items.Except(other.Items).Any() &&
                   !this.PropertyItems.Except(other.PropertyItems).Any();

            public override int GetHashCode() => HashCode.Combine(this.Items, this.PropertyItems);
        }
    }
}
