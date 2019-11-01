// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using NUnit.Framework;

    [TestFixture]
    public class IoTEdgeVstsAgentTest
    {
        [Test]
        public void TestConstructorWithInvalidIds([Values(-1, 0)] int id)
        {
            Assert.That(
                () =>
                    new IoTEdgeVstsAgent(
                        id,
                        "name",
                        "version",
                        VstsAgentStatus.Online,
                        true,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("Agent.OS", "Linux")
                        },
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-long-haul", "true")
                        }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TestConstructorWithInvalidNames([Values(null, "")] string name)
        {
            Assert.That(
                () =>
                    new IoTEdgeVstsAgent(
                        893,
                        name,
                        "version",
                        VstsAgentStatus.Online,
                        true,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("Agent.OS", "Linux")
                        },
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-long-haul", "true")
                        }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestConstructorWithInvalidVersion([Values(null, "")] string version)
        {
            Assert.That(
                () =>
                    new IoTEdgeVstsAgent(
                        893,
                        "name 123",
                        version,
                        VstsAgentStatus.Online,
                        true,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("Agent.OS", "Linux")
                        },
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-long-haul", "true")
                        }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        [TestCaseSource(typeof(IoTEdgeVstsAgentTestData), nameof(IoTEdgeVstsAgentTestData.InvalidSystemCapabilities))]
        public void TestConstructorWithInvalidSystemCapabilities(HashSet<AgentCapability> systemCapabilities)
        {
            Assert.That(
                () =>
                    new IoTEdgeVstsAgent(
                        893,
                        "name 123",
                        "version 1.23.3",
                        VstsAgentStatus.Online,
                        true,
                        systemCapabilities,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-long-haul", "true")
                        }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TestPorperties()
        {
            var iotedgeVstsAgent = new IoTEdgeVstsAgent(
                893,
                "name 123",
                "version 1.23.3",
                VstsAgentStatus.Online,
                true,
                new HashSet<AgentCapability>()
                {
                    new AgentCapability("Agent.OS", "Linux")
                },
                new HashSet<AgentCapability>()
                {
                    new AgentCapability("run-long-haul", "true")
                });

            Assert.AreEqual(893, iotedgeVstsAgent.Id);
            Assert.AreEqual("name 123", iotedgeVstsAgent.Name);
            Assert.AreEqual("version 1.23.3", iotedgeVstsAgent.Version);
            Assert.AreEqual(VstsAgentStatus.Online, iotedgeVstsAgent.Status);
            Assert.AreEqual(true, iotedgeVstsAgent.Enabled);
            Assert.Contains(new AgentCapability("Agent.OS", "Linux"), iotedgeVstsAgent.SystemCapabilities);
            Assert.Contains(new AgentCapability("run-long-haul", "true"), iotedgeVstsAgent.UserCapabilities);
        }

        [Test]
        public void TestGetHashCode()
        {
            var set = new HashSet<IoTEdgeVstsAgent>();

            set.Add(
                new IoTEdgeVstsAgent(
                    1001,
                    "name1",
                    "version1",
                    VstsAgentStatus.Online,
                    true,
                    new HashSet<AgentCapability>()
                    {
                        new AgentCapability("Agent.OS", "Linux")
                    },
                    new HashSet<AgentCapability>()
                    {
                        new AgentCapability("run-long-haul", "true")
                    }));
            set.Add(
                new IoTEdgeVstsAgent(
                    1002,
                    "name2",
                    "version2",
                    VstsAgentStatus.Online,
                    true,
                    new HashSet<AgentCapability>()
                    {
                        new AgentCapability("Agent.OS", "Windows")
                    },
                    new HashSet<AgentCapability>()
                    {
                        new AgentCapability("run-stress", "true")
                    }));

            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(
                set.Contains(
                    new IoTEdgeVstsAgent(
                        1001,
                        "name1",
                        "version1",
                        VstsAgentStatus.Online,
                        true,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("Agent.OS", "Linux")
                        },
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-long-haul", "true")
                        })));
            Assert.IsTrue(
                set.Contains(
                    new IoTEdgeVstsAgent(
                        1002,
                        "name2",
                        "version2",
                        VstsAgentStatus.Online,
                        true,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("Agent.OS", "Windows")
                        },
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-stress", "true")
                        })));
            Assert.IsFalse(
                set.Contains(
                    new IoTEdgeVstsAgent(
                        1003,
                        "name3",
                        "version3",
                        VstsAgentStatus.Online,
                        true,
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("Agent.OS", "Windows")
                        },
                        new HashSet<AgentCapability>()
                        {
                            new AgentCapability("run-stress", "true")
                        })));
        }

        public class IoTEdgeVstsAgentTestData
        {
            public static IEnumerable InvalidSystemCapabilities
            {
                get
                {
                    yield return new TestCaseData(null);
                    yield return new TestCaseData(new HashSet<AgentCapability>());
                }
            }
        }
    }
}
