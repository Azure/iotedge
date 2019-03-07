// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class CollectionExTest
    {
        [Fact]
        public void TestGetOrElse()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "one" },
                { 2, "two" }
            };
            Assert.Equal("one", dict.GetOrElse(1, "default"));
            Assert.Equal("two", dict.GetOrElse(2, "default"));
            Assert.Equal("default", dict.GetOrElse(3, "default"));
        }

        [Fact]
        public void TestHeadOption()
        {
            IEnumerable<int> list1 = new[] { 1, 2, 3 };
            IList<int> list2 = new List<int> { 1 };
            // ReSharper disable once CollectionNeverUpdated.Local
            IList<int> list3 = new List<int>();

            Assert.Equal(Option.Some(1), list1.HeadOption());
            Assert.Equal(Option.Some(1), list2.HeadOption());
            Assert.Equal(Option.None<int>(), list3.HeadOption());
            Assert.Equal(Option.Some(4), Enumerable.Repeat(4, 1).HeadOption());
            Assert.Equal(Option.None<int>(), Enumerable.Repeat(4, 0).HeadOption());
        }

        [Fact]
        public void TestGet()
        {
            var dict = new Dictionary<string, string> { { "key", "value" } };
            Assert.Equal(Option.Some("value"), dict.Get("key"));
            Assert.Equal(Option.None<string>(), dict.Get("not-there"));
        }

        [Fact]
        public void TestToLogString()
        {
            var dictionary = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" }
            };

            string str = dictionary.ToLogString();
            Assert.Equal("(k1, v1), (k2, v2)", str);
        }

        [Fact]
        public void TestToDictionary()
        {
            var stringsList = new List<string>()
            {
                "k1=v1",
                "k2=v2",
                "key 3  =  this is a value",
                "connstring=hostname=foo"
            };

            IDictionary<string, string> dictionary = stringsList.ToDictionary('=');

            Assert.NotNull(dictionary);
            Assert.Equal(4, dictionary.Count);
            Assert.Equal("v1", dictionary["k1"]);
            Assert.Equal("v2", dictionary["k2"]);
            Assert.Equal("  this is a value", dictionary["key 3  "]);
            Assert.Equal("hostname=foo", dictionary["connstring"]);
        }

        [Fact]
        public void TestToDictionaryThrows()
        {
            var stringsList = new List<string>()
            {
                "k1=v1",
                "key 3 this is a value"
            };

            Assert.Throws<FormatException>(() => stringsList.ToDictionary('='));
        }

        [Fact]
        public void TestElementAtOrDefault()
        {
            var source = new[] { "a", "b" };

            Assert.Equal("*", source.ElementAtOrDefault(-1, "*"));
            Assert.Equal("a", source.ElementAtOrDefault(0, "*"));
            Assert.Equal("b", source.ElementAtOrDefault(1, "*"));
            Assert.Equal("*", source.ElementAtOrDefault(2, "*"));
        }

        [Fact]
        public void TestElementAtOrEmpty()
        {
            var source = new[] { "a", "b" };

            Assert.Equal(string.Empty, source.ElementAtOrEmpty(-1));
            Assert.Equal("a", source.ElementAtOrEmpty(0));
            Assert.Equal("b", source.ElementAtOrEmpty(1));
            Assert.Equal(string.Empty, source.ElementAtOrEmpty(2));
        }

        [Fact]
        public void TestTryDictionaryRemove()
        {
            var map = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", "v3" },
            };

            Assert.True(map.TryRemove("k2", out string val) && val == "v2");
            Assert.False(map.TryRemove("booyah", out string _));
        }

        [Theory]
        [InlineData("foo", "bar", true)]
        [InlineData("foo", "", false)]
        [InlineData("", "bar", false)]
        [InlineData(null, "bar", false)]
        [InlineData("foo", null, false)]
        [InlineData(null, null, false)]
        public void AddIfNonEmptyTest_Strings(string key, string value, bool shouldAdd)
        {
            var dictionary = new Dictionary<string, string>();
            dictionary.AddIfNonEmpty(key, value);
            if (shouldAdd)
            {
                Assert.Equal(dictionary[key], value);
            }
            else if (key != null)
            {
                Assert.False(dictionary.ContainsKey(key));
            }
        }

        [Fact]
        public void AddIfNonEmptyTest_Objects()
        {
            var object1 = new object();
            var object2 = new object();

            var dictionary = new Dictionary<object, object>();
            dictionary.AddIfNonEmpty(object1, object2);
            Assert.Equal(dictionary[object1], object2);

            dictionary = new Dictionary<object, object>();
            dictionary.AddIfNonEmpty(null, object2);
            Assert.False(dictionary.ContainsKey(object1));

            dictionary = new Dictionary<object, object>();
            dictionary.AddIfNonEmpty(object1, null);
            Assert.False(dictionary.ContainsKey(object1));
        }

        [Fact]
        public void TryGetNonEmptyValueTest()
        {
            var stringDictionary = new Dictionary<string, string>
            {
                ["1"] = "Foo",
                ["2"] = string.Empty,
                ["3"] = "  ",
                ["4"] = null,
            };

            Assert.True(stringDictionary.TryGetNonEmptyValue("1", out string returnedString));
            Assert.Equal("Foo", returnedString);
            Assert.False(stringDictionary.TryGetNonEmptyValue("2", out returnedString));
            Assert.False(stringDictionary.TryGetNonEmptyValue("3", out returnedString));
            Assert.False(stringDictionary.TryGetNonEmptyValue("4", out returnedString));

            var obj = new object();
            var objectDictionary = new Dictionary<string, object>
            {
                ["1"] = obj,
                ["2"] = null
            };
            Assert.True(objectDictionary.TryGetNonEmptyValue("1", out object returnedObj));
            Assert.Equal(obj, returnedObj);
            Assert.False(objectDictionary.TryGetNonEmptyValue("2", out returnedObj));
        }
    }
}
