// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Util
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class OptionTest
    {
        [Fact]
        [Unit]
        public void TestCreate()
        {
            Option<int> some = Option.Some(2);
            Assert.True(some.HasValue);

            Option<int> none = Option.None<int>();
            Assert.False(none.HasValue);
        }

        [Fact]
        [Unit]
        public void TestEquals()
        {
            Option<int> some1 = Option.Some(1);
            Option<int> some2 = Option.Some(1);
            Option<int> some3 = Option.Some(2);
            Option<int> none1 = Option.None<int>();
            Option<int> none2 = Option.None<int>();

            Assert.True(some1.Equals(some1));
            Assert.True(some1 == some2);
            Assert.False(some1 != some2);
            Assert.False(some1 == some3);
            Assert.True(some1.Equals(some2));
            Assert.False(some1.Equals(some3));
            Assert.False(some1.Equals(none1));
            Assert.False(none1.Equals(some1));
            Assert.True(some1.Equals((object)some1));
            Assert.True(some1.Equals((object)some2));
            Assert.False(some1.Equals((object)some3));
            Assert.False(some1.Equals(new object()));

            Assert.Equal(none1, none2);
        }

        [Fact]
        [Unit]
        public void TestHashCode()
        {
            Option<int> some1 = Option.Some(1);
            Option<int> some2 = Option.Some(2);
            Option<object> some3 = Option.Some<object>(null);
            Option<int> none = Option.None<int>();

            Assert.Equal(0, none.GetHashCode());
            Assert.Equal(1, some3.GetHashCode());
            Assert.NotEqual(some1.GetHashCode(), some2.GetHashCode());
        }

        [Fact]
        [Unit]
        public void TestToString()
        {
            Option<int> some1 = Option.Some(1);
            Option<object> some2 = Option.Some<object>(null);
            Option<int> none = Option.None<int>();

            Assert.Equal("Some(1)", some1.ToString());
            Assert.Equal("Some(null)", some2.ToString());
            Assert.Equal("None", none.ToString());
        }

        [Fact]
        [Unit]
        public void TestEnumerable()
        {
            Option<int> some = Option.Some(6);
            Option<int> none = Option.None<int>();

            int i = 0;
            foreach (int value in some)
            {
                i += value;
            }

            Assert.Equal(6, i);

            int count = 0;
            // ReSharper disable once UnusedVariable
            foreach (int value in none)
            {
                count++;
            }

            Assert.Equal(0, count);

            Assert.Single(some.ToEnumerable());
            Assert.Empty(none.ToEnumerable());
        }

        [Fact]
        [Unit]
        public void TestContains()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();
            Option<object> some2 = Option.Some<object>(null);

            Assert.True(some.Contains(3));
            Assert.False(some.Contains(2));
            Assert.False(none.Contains(3));
            Assert.True(some2.Contains(null));
        }

        [Fact]
        [Unit]
        public void TestExists()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.True(some.Exists(v => v * 2 == 6));
            Assert.False(some.Exists(v => v * 2 == 7));
            Assert.False(none.Exists(_ => true));
        }

        [Fact]
        [Unit]
        public void TestGetOrElse()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.Equal(3, some.GetOrElse(4));
            Assert.Equal(2, none.GetOrElse(2));
        }

        [Fact]
        [Unit]
        public void TestElse()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.Equal(some, some.Else(Option.Some(4)));
            Assert.Equal(Option.Some(2), none.Else(Option.Some(2)));
        }

        [Fact]
        [Unit]
        public void TestOrDefault()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();
            Option<string> none2 = Option.None<string>();

            Assert.Equal(3, some.OrDefault());
            Assert.Equal(0, none.OrDefault());
            Assert.Null(none2.OrDefault());
        }

        [Fact]
        [Unit]
        public void TestMap()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.Equal(Option.Some(6), some.Map(v => v * 2));
            Assert.Equal(Option.None<int>(), none.Map(v => v * 2));
        }

        [Fact]
        [Unit]
        public void TestFlatMap()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.Equal(Option.Some(6), some.FlatMap(v => Option.Some(v * 2)));
            Assert.Equal(Option.None<int>(), none.FlatMap(v => Option.Some(v * 2)));
        }

        [Fact]
        [Unit]
        public void TestFilter()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.Equal(Option.Some(3), some.Filter(_ => true));
            Assert.Equal(Option.None<int>(), some.Filter(_ => false));
            Assert.Equal(Option.None<int>(), none.Filter(_ => false));
        }

        [Fact]
        [Unit]
        public void TestForEach()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            int i = 2;
            // ReSharper disable once AccessToModifiedClosure
            // Need to test the side effect
            some.ForEach(v => i *= v);
            Assert.Equal(6, i);

            i = 2;
            none.ForEach(v => i *= v);
            Assert.Equal(2, i);
        }
    }
}
