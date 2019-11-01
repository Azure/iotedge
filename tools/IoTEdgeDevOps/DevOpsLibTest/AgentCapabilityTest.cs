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
            Assert.IsTrue(set.Contains(new AgentCapability("c1", "v1")));
            Assert.IsTrue(set.Contains(new AgentCapability("c2", "v1")));
            Assert.IsFalse(set.Contains(new AgentCapability("c3", "v1")));
        }

        [Test]
        public void TestEquals()
        {
            var c1 = new AgentCapability("cap", "value");
            var c2 = new AgentCapability("CAP", "value");

            Assert.AreEqual(c1, c2);
        }

        [Test]
        public void TestClone()
        {
            var c1 = new AgentCapability("cap", "value");
            var c2 = c1.Clone();

            Assert.AreEqual(c1, c2);
        }
    }
}
