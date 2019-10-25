// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class AgentMatrix
    {
        class CapabilityKeys
        {
            public const string AgentOS = "Agent.OS";
            public const string AgentOSArchitecture = "Agent.OSArchitecture";
            public const string AgentOSName = "agent-os-name";
            public const string AgentOSBits = "agent-osbits";
            public const string RunnerOSName = "runner-os-name";
        }

        readonly IList<AgentDemandSet> rows = new List<AgentDemandSet>()
        {
            new AgentDemandSet(
                "Linux AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.AgentOS, "Linux"),
                    new AgentCapability(CapabilityKeys.AgentOSArchitecture, "X64"),
                }),
            new AgentDemandSet(
                "Linux ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.AgentOS, "Linux"),
                    new AgentCapability(CapabilityKeys.AgentOSArchitecture, "ARM"),
                    new AgentCapability(CapabilityKeys.AgentOSBits, "32"),
                }),
            new AgentDemandSet(
                "Linux ARM64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.AgentOS, "Linux"),
                    new AgentCapability(CapabilityKeys.AgentOSArchitecture, "ARM"),
                    new AgentCapability(CapabilityKeys.AgentOSBits, "64"),
                }),
            new AgentDemandSet(
                "Windows AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.AgentOS, "Windows_NT"),
                    new AgentCapability(CapabilityKeys.AgentOSArchitecture, "X64"),
                    new AgentCapability(CapabilityKeys.AgentOSName, "WinPro_x64"),
                }),
            new AgentDemandSet(
                "Windows Server Core AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.AgentOS, "Windows_NT"),
                    new AgentCapability(CapabilityKeys.AgentOSArchitecture, "X64"),
                    new AgentCapability(CapabilityKeys.AgentOSName, "WinServerCore_x64"),
                }),
            new AgentDemandSet(
                "Windows IoT Core AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.RunnerOSName, "WinIoTCore_x64"),
                }),
            new AgentDemandSet(
                "Windows IoT Core ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability(CapabilityKeys.RunnerOSName, "WinIoTCore_arm32"),
                }),
        };

        readonly IList<AgentDemandSet> columns = new List<AgentDemandSet>()
        {
            new AgentDemandSet(
                "Long Haul",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-long-haul", "true"),
                }),
            new AgentDemandSet(
                "Stress Test",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-stress", "true"),
                }),
            new AgentDemandSet(
                "E2E Tests",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("run-e2e-tests", "true"),
                }),
        };

        Dictionary<AgentDemandSet, Dictionary<AgentDemandSet, HashSet<IoTEdgeVstsAgent>>> matrix;
        Dictionary<IoTEdgeVstsAgent, bool> agents;

        public ImmutableList<AgentDemandSet> Rows => this.rows.ToImmutableList();

        public ImmutableList<AgentDemandSet> Columns => this.columns.ToImmutableList();

        public ImmutableDictionary<AgentDemandSet, Dictionary<AgentDemandSet, HashSet<IoTEdgeVstsAgent>>> Matrix =>
            this.matrix.ToImmutableDictionary();

        public void Update(HashSet<IoTEdgeVstsAgent> agents)
        {
            this.InitializeDimensions();

            this.agents = new Dictionary<IoTEdgeVstsAgent, bool>(agents.Select(a => new KeyValuePair<IoTEdgeVstsAgent, bool>((IoTEdgeVstsAgent)a.Clone(), false)));

            foreach (AgentDemandSet platform in this.rows)
            {
                foreach (AgentDemandSet testType in this.columns)
                {
                    foreach (IoTEdgeVstsAgent agent in this.GetUnmatchedAgents())
                    {
                        if (agent.Match(platform.Capabilities) && agent.Match(testType.Capabilities))
                        {
                            this.matrix[platform][testType].Add(agent);
                            this.agents[agent] = true;
                        }
                    }
                }
            }
        }

        public IoTEdgeVstsAgent[] GetUnmatchedAgents()
        {
            return this.agents.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToArray();
        }

        void InitializeDimensions()
        {
            this.matrix = new Dictionary<AgentDemandSet, Dictionary<AgentDemandSet, HashSet<IoTEdgeVstsAgent>>>();

            foreach (AgentDemandSet rowGroup in this.rows)
            {
                this.matrix.Add(rowGroup, new Dictionary<AgentDemandSet, HashSet<IoTEdgeVstsAgent>>());

                foreach (AgentDemandSet colGroup in this.columns)
                {
                    this.matrix[rowGroup].Add(colGroup, new HashSet<IoTEdgeVstsAgent>());
                }
            }
        }
    }
}
