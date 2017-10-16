// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using System;
    using System.Collections.Generic;
    using Xunit;

    [Unit]
    public class DictionaryComparerTest
    {
        [Fact]
        public void TestNullEquals()
        {
            var comparer = new DictionaryComparer<string, Person>();
            Assert.True(comparer.Equals(null, null));
        }

        [Fact]
        public void TestNullInequality()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", new Person("p2", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.False(comparer.Equals(null, d1));
            Assert.False(comparer.Equals(d1, null));
        }

        [Fact]
        public void TestRefEquals()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", new Person("p2", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.True(comparer.Equals(d1, d1));
        }

        [Fact]
        public void TestStringDictionaries()
        {
            var d1 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", null },
                { "k4", "v4" },
                { "k5", "v5" }
            };

            var d2 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k3", null },
                { "k2", "v2" },
                { "k5", "v5" },
                { "k4", "v4" }
            };

            Assert.True(DictionaryComparer.StringDictionaryComparer.Equals(d1, d2));
            Assert.True(DictionaryComparer.StringDictionaryComparer.Equals(d2, d1));
        }

        [Fact]
        public void TestStringDictionariesInequalValues()
        {
            var d1 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", null },
                { "k4", "v4" },
                { "k5", "v5" }
            };

            var d2 = new Dictionary<string, string>()
            {
                { "k1", "v11" },
                { "k3", null },
                { "k2", "v2" },
                { "k5", "v5" },
                { "k4", "v4" }
            };

            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d1, d2));
            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d2, d1));
        }

        [Fact]
        public void TestStringDictionariesInequalValues2()
        {
            var d1 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", null },
                { "k4", "v4" },
                { "k5", "v5" }
            };

            var d2 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k3", "v3" },
                { "k2", "v2" },
                { "k5", "v5" },
                { "k4", "v4" }
            };

            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d1, d2));
            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d2, d1));
        }

        [Fact]
        public void TestStringDictionariesInequalValues3()
        {
            var d1 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", "v3" },
                { "k4", "v4" },
                { "k5", "v5" }
            };

            var d2 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k3", null },
                { "k2", "v2" },
                { "k5", "v5" },
                { "k4", "v4" }
            };

            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d1, d2));
            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d2, d1));
        }

        [Fact]
        public void TestStringDictionariesInequalKeyCounts()
        {
            var d1 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", null },
                { "k4", "v4" },
                { "k5", "v5" }
            };

            var d2 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k5", "v5" },
                { "k4", "v4" }
            };

            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d1, d2));
            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d2, d1));
        }

        [Fact]
        public void TestStringDictionariesInequalKeys()
        {
            var d1 = new Dictionary<string, string>()
            {
                { "k1", "v1" },
                { "k2", "v2" },
                { "k3", null },
                { "k4", "v4" },
                { "k5", "v5" }
            };

            var d2 = new Dictionary<string, string>()
            {
                { "k11", "v1" },
                { "k3", null },
                { "k2", "v2" },
                { "k5", "v5" },
                { "k4", "v4" }
            };

            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d1, d2));
            Assert.False(DictionaryComparer.StringDictionaryComparer.Equals(d2, d1));
        }

        [Fact]
        public void TestObjectDictionaries()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", null },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var d2 = new Dictionary<string, Person>()
            {
                { "k5", new Person("p5", 50) },
                { "k1", new Person("p1", 10) },
                { "k3", new Person("p3", 30) },
                { "k2", null },
                { "k4", new Person("p4", 40) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.True(comparer.Equals(d1, d2));
            Assert.True(comparer.Equals(d2, d1));
        }

        [Fact]
        public void TestObjectDictionariesInequalValues()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", null },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var d2 = new Dictionary<string, Person>()
            {
                { "k5", new Person("p5", 50) },
                { "k1", new Person("p1", 110) },
                { "k3", new Person("p3", 30) },
                { "k2", null },
                { "k4", new Person("p4", 40) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.False(comparer.Equals(d1, d2));
            Assert.False(comparer.Equals(d2, d1));
        }

        [Fact]
        public void TestObjectDictionariesInequalValues2()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", null },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var d2 = new Dictionary<string, Person>()
            {
                { "k5", new Person("p5", 50) },
                { "k1", new Person("p1", 10) },
                { "k3", new Person("p3", 30) },
                { "k2", new Person("p3", 20) },
                { "k4", new Person("p4", 40) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.False(comparer.Equals(d1, d2));
            Assert.False(comparer.Equals(d2, d1));
        }

        [Fact]
        public void TestObjectDictionariesInequalValues3()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", new Person("p3", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var d2 = new Dictionary<string, Person>()
            {
                { "k5", new Person("p5", 50) },
                { "k1", new Person("p1", 10) },
                { "k3", new Person("p3", 30) },
                { "k2", null },
                { "k4", new Person("p4", 40) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.False(comparer.Equals(d1, d2));
            Assert.False(comparer.Equals(d2, d1));
        }

        [Fact]
        public void TestObjectDictionariesInequalKeyCounts()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", new Person("p3", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var d2 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", new Person("p3", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.False(comparer.Equals(d1, d2));
            Assert.False(comparer.Equals(d2, d1));
        }

        [Fact]
        public void TestObjectDictionariesInequalKeys()
        {
            var d1 = new Dictionary<string, Person>()
            {
                { "k1", new Person("p1", 10) },
                { "k2", new Person("p3", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var d2 = new Dictionary<string, Person>()
            {
                { "k11", new Person("p1", 10) },
                { "k2", new Person("p3", 20) },
                { "k3", new Person("p3", 30) },
                { "k4", new Person("p4", 40) },
                { "k5", new Person("p5", 50) },
            };

            var comparer = new DictionaryComparer<string, Person>();
            Assert.False(comparer.Equals(d1, d2));
            Assert.False(comparer.Equals(d2, d1));
        }

        public class Person : IEquatable<Person>
        {
            public Person(string name, int age)
            {
                this.Name = name ?? string.Empty;
                this.Age = age;
            }

            public string Name { get; }

            public int Age { get; }

            public bool Equals(Person other) =>
                other != null &&
                this.Name.Equals(other.Name) &&
                this.Age == other.Age;
        }
    }
}
