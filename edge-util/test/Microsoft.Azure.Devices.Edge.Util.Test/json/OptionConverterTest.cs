// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Json
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class OptionConverterTest
    {
        [Fact]
        public void TestSerializeWithValue()
        {
            var t1 = new TestObject("hello");
            string output = JsonConvert.SerializeObject(t1);
            string expected = "{\"value\":\"hello\"}";
            Assert.Equal(expected, output);
        }

        [Fact]
        public void TestSerializeWithoutValue()
        {
            var t1 = new TestObject(null);
            string output = JsonConvert.SerializeObject(t1);
            string expected = "{\"value\":null}";
            Assert.Equal(expected, output);
        }

        [Fact]
        public void TestDeserializeWithValue()
        {
            string input = "{\"value\":\"hello\"}";
            var t1 = JsonConvert.DeserializeObject<TestObject>(input);
            var expected = new TestObject("hello");
            Assert.Equal(expected, t1);
            Assert.Equal(Option.Some("hello"), t1.Value);
        }

        [Fact]
        public void TestDeserializeWithNullValue()
        {
            string input = "{\"value\":null}";
            var t1 = JsonConvert.DeserializeObject<TestObject>(input);
            var expected = new TestObject(null);
            Assert.Equal(expected, t1);
            Assert.Equal(Option.None<string>(), t1.Value);
        }

        [Fact]
        public void TestDeserializeWithoutValue()
        {
            string input = "{}";
            var t1 = JsonConvert.DeserializeObject<TestObject>(input);
            var expected = new TestObject(null);
            Assert.Equal(expected, t1);
            Assert.Equal(Option.None<string>(), t1.Value);
        }

        class TestObject : IEquatable<TestObject>
        {
            [JsonConstructor]
            public TestObject(string value)
            {
                this.Value = Option.Maybe(value);
            }

            [JsonConverter(typeof(OptionConverter<string>))]
            [JsonProperty(PropertyName = "value")]
            public Option<string> Value { get; }

            public bool Equals(TestObject other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                return ReferenceEquals(this, other) || this.Value.Equals(other.Value);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return obj.GetType() == this.GetType() && this.Equals((TestObject)obj);
            }

            public override int GetHashCode() => this.Value.GetHashCode();
        }
    }
}
