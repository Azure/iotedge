// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;

    public class AgentManagement
    {
        public const int IoTEdgeAgentPoolId = 123;
        const string AgentListPathSegmentFormat = "{0}/_apis/distributedtask/pools/{1}/agents";
        const string AgentPathSegmentFormat = "{0}/_apis/distributedtask/pools/{1}/agents/{2}";

        readonly DevOpsAccessSetting accessSetting;

        public AgentManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        public async Task<IList<VstsAgent>> GetAgentsAsync(int poolId)
        {
            string requestPath = string.Format(AgentListPathSegmentFormat, DevOpsAccessSetting.AzureOrganization, poolId);
            IFlurlRequest agentListRequest = DevOpsAccessSetting.BaseUrl
                .AppendPathSegment(requestPath)
                .SetQueryParam("api-version", "5.1")
                .WithBasicAuth(string.Empty, this.accessSetting.MsazurePAT);

            VstsAgentList agentList = await agentListRequest.GetJsonAsync<VstsAgentList>().ConfigureAwait(false);

            // Get capabilities for each agent
            var agentsWithCapabilities = new List<VstsAgent>();
            foreach (int agentId in agentList.Value.Select(a => a.Id))
            {
                agentsWithCapabilities.Add(await this.GetAgentAsync(poolId, agentId).ConfigureAwait(false));
            }

            return agentsWithCapabilities;
        }

        async Task<VstsAgent> GetAgentAsync(int poolId, int agentId)
        {
            string requestPath = string.Format(AgentPathSegmentFormat, DevOpsAccessSetting.AzureOrganization, poolId, agentId);
            IFlurlRequest agentRequest = DevOpsAccessSetting.BaseUrl
                .AppendPathSegment(requestPath)
                .SetQueryParam("api-version", "5.1")
                .SetQueryParam("includeCapabilities", "true")
                .WithBasicAuth(string.Empty, this.accessSetting.MsazurePAT);

            return await agentRequest.GetJsonAsync<VstsAgent>().ConfigureAwait(false);
        }
    }
}
