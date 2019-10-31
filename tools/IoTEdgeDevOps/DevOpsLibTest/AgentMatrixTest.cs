// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using NUnit.Framework;

    [TestFixture]
    public class AgentMatrixTest
    {
        [Test]
        public void TestUpdate()
        {
            var agents = new HashSet<IoTEdgeVstsAgent>();
            var agent1 = new IoTEdgeVstsAgent(
                38324,
                "iotedge-pi-04",
                "version",
                VstsAgentStatus.Online,
                true,
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("Agent.OSArchitecture", "ARM"),
                },
                new HashSet<AgentCapability>
                {
                    new AgentCapability("agent-osbits", "32"),
                    new AgentCapability("run-stress", "true"),
                });
            agents.Add(agent1);

            var agent2 = new IoTEdgeVstsAgent(
                39472,
                "iotedge-win5",
                "version",
                VstsAgentStatus.Online,
                true,
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Windows_NT"),
                    new AgentCapability("Agent.OSArchitecture", "X64"),
                },
                new HashSet<AgentCapability>
                {
                    new AgentCapability("agent-os-name", "WinPro_x64"),
                    new AgentCapability("run-long-haul", "true"),
                });
            agents.Add(agent2);

            var agentMatrix = new AgentMatrix();
            agentMatrix.Update(agents);

            var rowGroup1 = new AgentDemandSet(
                "Linux ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("Agent.OSArchitecture", "ARM"),
                    new AgentCapability("agent-osbits", "32"),
                });

            var colGroup1 = new AgentDemandSet(
                "Stress Test",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-stress", "true"),
                });

            var rowGroup2 = new AgentDemandSet(
                "Windows AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Windows_NT"),
                    new AgentCapability("Agent.OSArchitecture", "X64"),
                    new AgentCapability("agent-os-name", "WinPro_x64"),
                });

            var colGroup2 = new AgentDemandSet(
                "Long Haul",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-long-haul", "true"),
                });

            ImmutableDictionary<AgentDemandSet, Dictionary<AgentDemandSet, HashSet<IoTEdgeVstsAgent>>> updatedMatrix = agentMatrix.Matrix;

            foreach (AgentDemandSet row in updatedMatrix.Keys)
            {
                foreach (AgentDemandSet column in updatedMatrix[row].Keys)
                {
                    if (row.Equals(rowGroup1) && column.Equals(colGroup1))
                    {
                        Assert.AreEqual(1, updatedMatrix[row][column].Count);
                    }
                    else if (row.Equals(rowGroup2) && column.Equals(colGroup2))
                    {
                        Assert.AreEqual(1, updatedMatrix[row][column].Count);
                    }
                    else
                    {
                        Assert.AreEqual(0, updatedMatrix[row][column].Count);
                    }
                }
            }
        }
    }
}
