// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class AgentMatrix
    {
        readonly IList<AgentDemandSet> rows = new List<AgentDemandSet>()
        {
            new AgentDemandSet(
                "Linux AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("Agent.OSArchitecture", "X64"),
                }),
            new AgentDemandSet(
                "Linux ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("Agent.OSArchitecture", "ARM"),
                    new AgentCapability("agent-osbits", "32"),
                }),
            new AgentDemandSet(
                "Linux ARM64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Linux"),
                    new AgentCapability("Agent.OSArchitecture", "ARM"),
                    new AgentCapability("agent-osbits", "64"),
                }),
            new AgentDemandSet(
                "Windows AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Windows_NT"),
                    new AgentCapability("Agent.OSArchitecture", "X64"),
                    new AgentCapability("agent-os-name", "WinPro_x64"),
                }),
            new AgentDemandSet(
                "Windows Server Core AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("Agent.OS", "Windows_NT"),
                    new AgentCapability("Agent.OSArchitecture", "X64"),
                    new AgentCapability("agent-os-name", "WinServerCore_x64"),
                }),
            new AgentDemandSet(
                "Windows IoT Core AMD64",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("runner-os-name", "WinIoTCore_x64"),
                }),
            new AgentDemandSet(
                "Windows IoT Core ARM32",
                new HashSet<AgentCapability>
                {
                    new AgentCapability("runner-os-name", "WinIoTCore_arm32"),
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
