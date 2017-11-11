// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using System;

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

            var dictionary = stringsList.ToDictionary('=');

            Assert.NotNull(dictionary);
            Assert.Equal(4, dictionary.Count);
            Assert.Equal(dictionary["k1"], "v1");
            Assert.Equal(dictionary["k2"], "v2");
            Assert.Equal(dictionary["key 3  "], "  this is a value");
            Assert.Equal(dictionary["connstring"], "hostname=foo");
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
            var source = new string[] { "a", "b" };

            Assert.Equal("*", source.ElementAtOrDefault(-1, "*"));
            Assert.Equal("a", source.ElementAtOrDefault(0, "*"));
            Assert.Equal("b", source.ElementAtOrDefault(1, "*"));
            Assert.Equal("*", source.ElementAtOrDefault(2, "*"));
        }

        [Fact]
        public void TestElementAtOrEmpty()
        {
            var source = new string[] { "a", "b" };

            Assert.Equal(string.Empty, source.ElementAtOrEmpty(-1));
            Assert.Equal("a", source.ElementAtOrEmpty(0));
            Assert.Equal("b", source.ElementAtOrEmpty(1));
            Assert.Equal(string.Empty, source.ElementAtOrEmpty(2));
        }
    }
}
