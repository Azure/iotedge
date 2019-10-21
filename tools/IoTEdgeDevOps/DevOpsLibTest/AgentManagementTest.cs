// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using Flurl.Http.Testing;
    using NUnit.Framework;

    [TestFixture]
    public class AgentManagementTest
    {
        const string PersonalAccessToken = "pattoken123";

        AgentManagement agentManagement;
        HttpTest httpTest;

        [SetUp]
        public void Setup()
        {
            this.httpTest = new HttpTest();
            this.agentManagement = new AgentManagement(new DevOpsAccessSetting(PersonalAccessToken));
        }

        [TearDown]
        public void TearDown()
        {
            this.httpTest.Dispose();
        }

        [Test]
        public async Task TestGetAgentsAsync()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    count = 2,
                    value = new[]
                    {
                        new { id = 1345, name = "agent1", version = "2.152.1", status = "online", enabled = "true" },
                        new { id = 78493, name = "agent2", version = "2.150.0", status = "offline", enabled = "false" }
                    }
                });

            this.httpTest.RespondWithJson(
                new
                {
                    id = 1345,
                    name = "agent1",
                    version = "2.152.1",
                    status = "online",
                    enabled = "true",
                    systemCapabilities = new Dictionary<string, string>
                    {
                        { "syscap1_1", "syscap1_1_value" },
                        { "syscap1_2", "syscap1_2_value" },
                    },
                    userCapabilities = new Dictionary<string, string>
                    {
                        { "usercap1_1", "usercap1_1_value" },
                    },
                });

            this.httpTest.RespondWithJson(
                new
                {
                    id = 78493,
                    name = "agent2",
                    version = "2.150.0",
                    status = "offline",
                    enabled = "false",
                    systemCapabilities = new Dictionary<string, string>
                    {
                        { "syscap2_1", "syscap2_1_value" },
                    },
                    userCapabilities = new Dictionary<string, string>
                    {
                        { "usercap2_1", "usercap2_1_value" },
                        { "usercap2_2", "usercap2_2_value" },
                    },
                });

            int poolId = 123; // Azure-IoT-Edge-Core pool id
            IList<VstsAgent> agents = await this.agentManagement.GetAgentsAsync(poolId).ConfigureAwait(false);

            this.httpTest.ShouldHaveCalled($"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/_apis/distributedtask/pools/{poolId}/agents?api-version=5.1")
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            this.httpTest.ShouldHaveCalled($"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/_apis/distributedtask/pools/{poolId}/agents/1345?api-version=5.1&includeCapabilities=true")
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            this.httpTest.ShouldHaveCalled($"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/_apis/distributedtask/pools/{poolId}/agents/78493?api-version=5.1&includeCapabilities=true")
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(2, agents.Count);

            Assert.AreEqual(1345, agents[0].Id);
            Assert.AreEqual("agent1", agents[0].Name);
            Assert.AreEqual("2.152.1", agents[0].Version);
            Assert.AreEqual(VstsAgentStatus.Online, agents[0].Status);
            Assert.AreEqual(true, agents[0].Enabled);
            Assert.AreEqual(2, agents[0].SystemCapabilities.Count);
            Assert.AreEqual("syscap1_1_value", agents[0].SystemCapabilities["syscap1_1"]);
            Assert.AreEqual("syscap1_2_value", agents[0].SystemCapabilities["syscap1_2"]);
            Assert.AreEqual(1, agents[0].UserCapabilities.Count);
            Assert.AreEqual("usercap1_1_value", agents[0].UserCapabilities["usercap1_1"]);

            Assert.AreEqual(78493, agents[1].Id);
            Assert.AreEqual("agent2", agents[1].Name);
            Assert.AreEqual("2.150.0", agents[1].Version);
            Assert.AreEqual(VstsAgentStatus.Offline, agents[1].Status);
            Assert.AreEqual(false, agents[1].Enabled);
            Assert.AreEqual(1, agents[1].SystemCapabilities.Count);
            Assert.AreEqual("syscap2_1_value", agents[1].SystemCapabilities["syscap2_1"]);
            Assert.AreEqual(2, agents[1].UserCapabilities.Count);
            Assert.AreEqual("usercap2_1_value", agents[1].UserCapabilities["usercap2_1"]);
            Assert.AreEqual("usercap2_2_value", agents[1].UserCapabilities["usercap2_2"]);
        }
    }
}
