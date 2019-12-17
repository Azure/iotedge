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
            var agents = new HashSet<IoTEdgeAgent>();
            var agent1 = new IoTEdgeAgent(
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

            var agent2 = new IoTEdgeAgent(
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

            ImmutableDictionary<AgentDemandSet, Dictionary<AgentDemandSet, HashSet<IoTEdgeAgent>>> updatedMatrix = agentMatrix.Matrix;

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

        [Test]
        public void TestProperties()
        {
            // Rows
            var linuxAMD64ds = new AgentDemandSet(
                "Linux AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.AgentOS, "Linux"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSArchitecture, "X64"),
                });
            var linuxARM32ds = new AgentDemandSet(
                "Linux ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.AgentOS, "Linux"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSArchitecture, "ARM"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSBits, "32"),
                });
            var linuxARM64ds = new AgentDemandSet(
                "Linux ARM64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.AgentOS, "Linux"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSArchitecture, "ARM"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSBits, "64"),
                });
            var windowsARM64ds = new AgentDemandSet(
                "Windows AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.AgentOS, "Windows_NT"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSArchitecture, "X64"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSName, "WinPro_x64"),
                });
            var windowsServerCoreAMD64ds = new AgentDemandSet(
                "Windows Server Core AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.AgentOS, "Windows_NT"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSArchitecture, "X64"),
                    new AgentCapability(AgentCapabilityKeys.AgentOSName, "WinServerCore_x64"),
                });
            var windowsIoTCoreAMD64ds = new AgentDemandSet(
                "Windows IoT Core AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.RunnerOSName, "WinIoTCore_x64"),
                });
            var windowsIoTCoreARM32ds = new AgentDemandSet(
                "Windows IoT Core ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(AgentCapabilityKeys.RunnerOSName, "WinIoTCore_arm32"),
                });

            // Columns
            var longHaulds = new AgentDemandSet(
                "Long Haul",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-long-haul", "true"),
                });
            var stressTestds = new AgentDemandSet(
                "Stress Test",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-stress", "true"),
                });
            var e2eTestsds = new AgentDemandSet(
                "E2E Tests",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-e2e-tests", "true"),
                });

            var agentMatrix = new AgentMatrix();

            var rows = agentMatrix.Rows;
            var columns = agentMatrix.Columns;

            Assert.AreEqual(7, rows.Count);
            Assert.AreEqual(3, columns.Count);

            Assert.True(linuxAMD64ds.Equals(rows[0]));
            Assert.True(linuxARM32ds.Equals(rows[1]));
            Assert.True(linuxARM64ds.Equals(rows[2]));
            Assert.True(windowsARM64ds.Equals(rows[3]));
            Assert.True(windowsServerCoreAMD64ds.Equals(rows[4]));
            Assert.True(windowsIoTCoreAMD64ds.Equals(rows[5]));
            Assert.True(windowsIoTCoreARM32ds.Equals(rows[6]));

            Assert.True(longHaulds.Equals(columns[0]));
            Assert.True(stressTestds.Equals(columns[1]));
            Assert.True(e2eTestsds.Equals(columns[2]));
        }
    }
}
