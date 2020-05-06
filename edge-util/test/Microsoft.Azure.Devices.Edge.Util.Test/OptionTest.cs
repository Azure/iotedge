// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

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

            Option<string> maybeNull = Option.Maybe<string>(null);
            Assert.False(maybeNull.HasValue);

            Option<string> maybeNotNull = Option.Maybe("boo");
            Assert.True(maybeNotNull.HasValue);

            Option<int> maybeIntNull = Option.Maybe<int>(null);
            Assert.False(maybeIntNull.HasValue);

            int? nullableInt = 2;
            Option<int> maybeIntNotNull = Option.Maybe(nullableInt);
            Assert.True(maybeIntNotNull.HasValue);
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
            Option<object> some3 = Option.None<object>();
            Option<int> none = Option.None<int>();

            Assert.Equal(0, none.GetHashCode());
            Assert.Equal(0, some3.GetHashCode());
            Assert.NotEqual(some1.GetHashCode(), some2.GetHashCode());
        }

        [Fact]
        [Unit]
        public void TestToString()
        {
            Option<int> some1 = Option.Some(1);
            Option<object> some2 = Option.Some<object>(new object());
            Option<int> none = Option.None<int>();

            Assert.Equal("Some(1)", some1.ToString());
            Assert.Equal("Some(System.Object)", some2.ToString());
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

            int someCount = some.ToEnumerable().Count();
            Assert.Equal(1, someCount);
            int noneCount = none.ToEnumerable().Count();
            Assert.Equal(0, noneCount);
        }

        [Fact]
        [Unit]
        public void TestContains()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();
            var value = new object();
            Option<object> some2 = Option.Some<object>(value);

            Assert.True(some.Contains(3));
            Assert.False(some.Contains(2));
            Assert.False(none.Contains(3));
            Assert.True(some2.Contains(value));
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
            Assert.Equal(1, none.GetOrElse(() => 1));
        }

        [Fact]
        [Unit]
        public void TestElse()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            Assert.Equal(some, some.Else(Option.Some(4)));
            Assert.Equal(some, some.Else(() => Option.Some(4)));
            Assert.Equal(Option.Some(2), none.Else(Option.Some(2)));
            Assert.Equal(Option.Some(2), none.Else(() => Option.Some(2)));
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

        [Fact]
        [Unit]
        public void TestForEachWithNoneMethod()
        {
            int val = 0;
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();
            some.ForEach(
            b => { val += b; },
            () => { val = -1; });
            Assert.Equal(3, val);

            val = 0;
            none.ForEach(
            b => { val += b; },
            () => { val = -1; });
            Assert.Equal(-1, val);
        }

        [Fact]
        [Unit]
        public async void TestForEachAsync()
        {
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();

            int i = 2;
            // ReSharper disable once AccessToModifiedClosure
            // Need to test the side effect
            await some.ForEachAsync(
                v =>
                {
                    i *= v;
                    return Task.CompletedTask;
                });
            Assert.Equal(6, i);

            i = 2;
            await none.ForEachAsync(
                v =>
                {
                    i *= v;
                    return Task.CompletedTask;
                });
            Assert.Equal(2, i);
        }

        [Fact]
        [Unit]
        public async void TestForEachWithNoneMethodAsync()
        {
            var val = 0;
            Option<int> some = Option.Some(3);
            Option<int> none = Option.None<int>();
            await some.ForEachAsync(
            b =>
            {
                val += b;
                return Task.CompletedTask;
            },
            () =>
            {
                val = -2;
                return Task.CompletedTask;
            });
            Assert.Equal(3, val);

            val = 0;
            await none.ForEachAsync(
            b =>
            {
                val += b;
                return Task.CompletedTask;
            },
            () =>
            {
                val = -2;
                return Task.CompletedTask;
            });
            Assert.Equal(-2, val);
        }

        [Fact]
        [Unit]
        public void TestFilterMap_NoPredicate_DropsNones()
        {
            var values = new Option<int>[]
                         {
                            Option.Some(1),
                            Option.Some(2),
                            Option.None<int>(),
                            Option.Some(3),
                            Option.None<int>(),
                         };

            var result = values.FilterMap().ToList();

            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        [Fact]
        [Unit]
        public void TestFilterMap_PredicateFilters_and_DropsNones()
        {
            var values = new Option<int>[]
                         {
                            Option.Some(1),
                            Option.Some(2),
                            Option.None<int>(),
                            Option.Some(3),
                            Option.None<int>(),
                            Option.Some(4)
                         };

            var result = values.FilterMap(x => x % 2 == 0).ToList();

            Assert.Equal(new[] { 2, 4 }, result);
        }

        [Fact]
        [Unit]
        public void TestMatchFull()
        {
            Option<int> intOption = Option.Some(0);
            int newTestNum = intOption.Match(b => b + 1, () => -1);
            Assert.Equal(1, newTestNum);
        }

        [Fact]
        [Unit]
        public void TestMatchEmpty()
        {
            Option<int> intOption = Option.None<int>();
            int newTestNum = intOption.Match(b => b + 1, () => -1);
            Assert.Equal(-1, newTestNum);
        }
    }
}
