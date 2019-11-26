// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using DevOpsLib;
    using NUnit.Framework;

    [TestFixture]
    public class AgentCapabilityTest
    {
        [Test]
        public void TestConstructorWithNoName()
        {
            Assert.That(() => new AgentCapability(null, "value"), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithEmptyName()
        {
            Assert.That(() => new AgentCapability(string.Empty, "value"), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithNoValue()
        {
            Assert.That(() => new AgentCapability("name", null), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithEmptyValue()
        {
            Assert.That(() => new AgentCapability("name", string.Empty), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestPorperties()
        {
            var ac = new AgentCapability("cap123", "value 456");

            Assert.AreEqual("cap123", ac.Name);
            Assert.AreEqual("value 456", ac.Value);
        }

        [Test]
        public void TestGetHashCode()
        {
            var set = new HashSet<AgentCapability>();

            set.Add(new AgentCapability("c1", "v1"));
            set.Add(new AgentCapability("c2", "v1"));

            Assert.AreEqual(2, set.Count);
            Assert.True(set.Contains(new AgentCapability("c1", "v1")));
            Assert.True(set.Contains(new AgentCapability("c2", "v1")));
            Assert.False(set.Contains(new AgentCapability("c3", "v1")));
        }

        [Test]
        public void TestEquals()
        {
            var c1 = new AgentCapability("cap", "value");
            var c2 = new AgentCapability("CAP", "value");

            Assert.False(c1.Equals(null));
            Assert.True(c1.Equals(c1));
            Assert.True(c1.Equals(c2));

            Assert.False(c1.Equals((object) null));
            Assert.True(c1.Equals((object) c1));
            Assert.True(c1.Equals((object) c2));
            Assert.False(c1.Equals(new object()));
        }

        [Test]
        public void TestClone()
        {
            var c1 = new AgentCapability("cap", "value");
            var c2 = c1.Clone();

            Assert.AreNotSame(c1, c2);
            Assert.AreEqual(c1, c2);
        }

        [Test]
        public void TestCompareTo()
        {
            var c1 = new AgentCapability("cap1", "value1");
            var c2 = new AgentCapability("cap1", "value1");
            var c3 = new AgentCapability("cap2", "value1");
            var c4 = new AgentCapability("cap1", "value2");
            var c5 = new AgentCapability("cap2", "value2");

            Assert.AreEqual(0, c1.CompareTo(c2));
            Assert.Greater(0, c1.CompareTo(c3));
            Assert.Less(0, c4.CompareTo(c1));
            Assert.Greater(0, c1.CompareTo(c5));
        }

        [Test]
        public void TestToString()
        {
            var c = new AgentCapability("cap", "value");

            Assert.AreEqual("cap=value", c.ToString());
        }
    }
}
