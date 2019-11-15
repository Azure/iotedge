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
    public class IoTEdgeAgentTest
    {
        [Test]
        public void TestConstructorWithInvalidIds([Values(-1, 0)] int id)
        {
            Assert.That(
                () =>
                    new IoTEdgeAgent(
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
                    new IoTEdgeAgent(
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
                    new IoTEdgeAgent(
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
                    new IoTEdgeAgent(
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
            var iotedgeAgent = new IoTEdgeAgent(
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

            Assert.AreEqual(893, iotedgeAgent.Id);
            Assert.AreEqual("name 123", iotedgeAgent.Name);
            Assert.AreEqual("version 1.23.3", iotedgeAgent.Version);
            Assert.AreEqual(VstsAgentStatus.Online, iotedgeAgent.Status);
            Assert.AreEqual(true, iotedgeAgent.Enabled);
            Assert.Contains(new AgentCapability("Agent.OS", "Linux"), iotedgeAgent.SystemCapabilities);
            Assert.Contains(new AgentCapability("run-long-haul", "true"), iotedgeAgent.UserCapabilities);
            Assert.True(iotedgeAgent.IsAvailable);
        }

        [Test]
        public void TestEquals()
        {
            var a1 = new IoTEdgeAgent(
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
                });
            var a2 = new IoTEdgeAgent(
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
                });

            Assert.False(a1.Equals(null));
            Assert.True(a1.Equals(a1));
            Assert.True(a1.Equals(a2));

            Assert.False(a1.Equals((object) null));
            Assert.True(a1.Equals((object)a1));
            Assert.True(a1.Equals((object) a2));
            Assert.False(a1.Equals(new object()));
        }

        [Test]
        public void TestGetHashCode()
        {
            var set = new HashSet<IoTEdgeAgent>();

            set.Add(
                new IoTEdgeAgent(
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
                new IoTEdgeAgent(
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
            Assert.True(
                set.Contains(
                    new IoTEdgeAgent(
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
            Assert.True(
                set.Contains(
                    new IoTEdgeAgent(
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
            Assert.False(
                set.Contains(
                    new IoTEdgeAgent(
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

        [Test]
        public void TestGetAgentGroupSuccess()
        {
            var agent1 = new IoTEdgeAgent(
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
                    new AgentCapability("run-long-haul", "true"),
                    new AgentCapability("agent-group", "2")
                });

            Assert.AreEqual("2", agent1.GetAgentGroup());
        }

        [Test]
        public void TestGetAgentGroupReturnsEmpty()
        {
            var agent1 = new IoTEdgeAgent(
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
                });

            Assert.AreEqual(string.Empty, agent1.GetAgentGroup());
        }

        [Test]
        public void TestClone()
        {
            var agent1 = new IoTEdgeAgent(
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
                    new AgentCapability("run-long-haul", "true"),
                    new AgentCapability("agent-group", "2")
                });

            var agent2 = agent1.Clone();

            Assert.AreNotSame(agent1, agent2);
            Assert.AreEqual(agent1, agent2);
        }

        [Test]
        public void TestCreate()
        {
            VstsAgent agent1 = new VstsAgent
            {
                Enabled = true,
                Id = 8474,
                Name = "iotedge-win-99",
                Status = VstsAgentStatus.Online,
                SystemCapabilities = new Dictionary<string, string>
                {
                    { "SysCap1Key", "SysCap1Value" },
                    { "SysCap2Key", "SysCap2Value" }
                },
                UserCapabilities = new Dictionary<string, string>
                {
                    { "UserCap1Key", "UserCap1Value" },
                    { "UserCap2Key", "UserCap2Value" }
                },
                Version = "version99"
            };

            IoTEdgeAgent agent2 = IoTEdgeAgent.Create(agent1);

            Assert.True(agent2.Enabled);
            Assert.AreEqual(8474, agent2.Id);
            Assert.AreEqual("iotedge-win-99", agent2.Name);
            Assert.AreEqual(VstsAgentStatus.Online, agent2.Status);
            Assert.AreEqual(2, agent2.SystemCapabilities.Count);
            Assert.True(agent2.SystemCapabilities.Contains(new AgentCapability("SysCap1Key", "SysCap1Value")));
            Assert.True(agent2.SystemCapabilities.Contains(new AgentCapability("SysCap2Key", "SysCap2Value")));
            Assert.AreEqual(2, agent2.UserCapabilities.Count);
            Assert.True(agent2.UserCapabilities.Contains(new AgentCapability("UserCap1Key", "UserCap1Value")));
            Assert.True(agent2.UserCapabilities.Contains(new AgentCapability("UserCap2Key", "UserCap2Value")));
            Assert.AreEqual("version99", agent2.Version);
        }

        [Test]
        public void TestMatch()
        {
            var agent1 = new IoTEdgeAgent(
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
                });

            bool match = agent1.Match(
                new AgentCapability[]
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("run-long-haul", "true")
                });
            Assert.True(match);

            match = agent1.Match(
                new AgentCapability[]
                {
                    new AgentCapability("Agent.OS", "Linux")
                });
            Assert.True(match);

            match = agent1.Match(
                new AgentCapability[]
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("run-stress", "true")
                });
            Assert.False(match);

            match = agent1.Match(
                new AgentCapability[]
                {
                    new AgentCapability("Agent.OS", "Windows"),
                    new AgentCapability("run-stress", "true")
                });
            Assert.False(match);
        }

        [Test]
        public void TestIsAvailable()
        {
            var agent1 = new IoTEdgeAgent(
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
                });

            var agent2 = new IoTEdgeAgent(
                1001,
                "name1",
                "version1",
                VstsAgentStatus.Online,
                false,
                new HashSet<AgentCapability>()
                {
                    new AgentCapability("Agent.OS", "Linux")
                },
                new HashSet<AgentCapability>()
                {
                    new AgentCapability("run-long-haul", "true")
                });

            var agent3 = new IoTEdgeAgent(
                1001,
                "name1",
                "version1",
                VstsAgentStatus.Offline,
                true,
                new HashSet<AgentCapability>()
                {
                    new AgentCapability("Agent.OS", "Linux")
                },
                new HashSet<AgentCapability>()
                {
                    new AgentCapability("run-long-haul", "true")
                });

            Assert.True(agent1.IsAvailable);
            Assert.False(agent2.IsAvailable);
            Assert.False(agent3.IsAvailable);
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
