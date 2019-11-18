// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DevOpsLib;
    using NUnit.Framework;

    [TestFixture]
    public class AgentDemandSetTest
    {
        [Test]
        public void TestConstructorWithNoName()
        {
            Assert.That(
                () => new AgentDemandSet(null, new HashSet<AgentCapability> { new AgentCapability("name", "value") }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithEmptyName()
        {
            Assert.That(
                () => new AgentDemandSet(string.Empty, new HashSet<AgentCapability> { new AgentCapability("name", "value") }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithNoValue()
        {
            Assert.That(() => new AgentDemandSet("name", null), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithEmptyValue()
        {
            Assert.That(() => new AgentDemandSet("name", new HashSet<AgentCapability>()), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestPorperties()
        {
            var mg = new AgentDemandSet(
                "Linux ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("agent-osbits", "32"),
                });

            Assert.AreEqual("Linux ARM32", mg.Name);
            var ac = mg.Capabilities.First();
            Assert.AreEqual("agent-osbits", ac.Name);
            Assert.AreEqual("32", ac.Value);
        }

        [Test]
        public void TestGetHashCode()
        {
            var set = new HashSet<AgentDemandSet>();

            set.Add(new AgentDemandSet("g1", new HashSet<AgentCapability> { new AgentCapability("c1", "v1") }));
            set.Add(new AgentDemandSet("g2", new HashSet<AgentCapability> { new AgentCapability("c2", "v2") }));

            Assert.AreEqual(2, set.Count);
            Assert.True(set.Contains(new AgentDemandSet("g1", new HashSet<AgentCapability> { new AgentCapability("c1", "v1") })));
            Assert.True(set.Contains(new AgentDemandSet("g2", new HashSet<AgentCapability> { new AgentCapability("c2", "v2") })));
            Assert.False(set.Contains(new AgentDemandSet("g2", new HashSet<AgentCapability> { new AgentCapability("cx", "v2") })));
            Assert.False(set.Contains(new AgentDemandSet("gx", new HashSet<AgentCapability> { new AgentCapability("c2", "v2") })));
        }

        [Test]
        public void TestEquals()
        {
            var g1 = new AgentDemandSet("g1", new HashSet<AgentCapability> { new AgentCapability("c1", "v1") });
            var g2 = new AgentDemandSet("g1", new HashSet<AgentCapability> { new AgentCapability("c1", "v1") });

            Assert.False(g1.Equals(null));
            Assert.True(g1.Equals(g1));
            Assert.True(g1.Equals(g2));

            Assert.False(g1.Equals((object)null));
            Assert.True(g1.Equals((object) g1));
            Assert.True(g1.Equals((object) g2));
            Assert.False(g1.Equals(new object()));
        }
    }
}
