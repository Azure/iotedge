// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test.Util
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class CollectionExTest
    {
        [Fact, Unit]
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

        [Fact, Unit]
        public void TestHeadOption()
        {
            IEnumerable<int> list1 = new [] { 1, 2, 3};
            IList<int> list2 = new List<int> { 1 };
            // ReSharper disable once CollectionNeverUpdated.Local
            IList<int> list3 = new List<int>();

            Assert.Equal(Option.Some(1), list1.HeadOption());
            Assert.Equal(Option.Some(1), list2.HeadOption());
            Assert.Equal(Option.None<int>(), list3.HeadOption());
            Assert.Equal(Option.Some(4), Enumerable.Repeat(4, 1).HeadOption());
            Assert.Equal(Option.None<int>(), Enumerable.Repeat(4, 0).HeadOption());
        }

        [Fact, Unit]
        public void TestGet()
        {
            var dict = new Dictionary<string, string> { { "key", "value" } };
            Assert.Equal(Option.Some("value"), dict.Get("key"));
            Assert.Equal(Option.None<string>(), dict.Get("not-there"));
        }
    }
}
