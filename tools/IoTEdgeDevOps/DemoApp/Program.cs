// Copyright (c) Microsoft. All rights reserved.
namespace DemoApp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;

    class Program
    {
        static async Task Main(string[] args)
        {
            var devOpsAccessSetting = new DevOpsAccessSetting("<PAT>");

            try
            {
                var releaseManagement = new ReleaseManagement(devOpsAccessSetting);
                List<IoTEdgeRelease> releases = await releaseManagement.GetReleasesAsync(ReleaseDefinitionId.E2ETest, "refs/heads/master");

                var agentManagement = new AgentManagement(devOpsAccessSetting);
                IList<VstsAgent> agents = await agentManagement.GetAgentsAsync(AgentManagement.IoTEdgeAgentPoolId).ConfigureAwait(false);

                var agentMatrix = new AgentMatrix();
                agentMatrix.Update(agents.Select(IoTEdgeAgent.Create).ToHashSet());
                var unmatchedAgents = agentMatrix.GetUnmatchedAgents();

                var buildManagement = new BuildManagement(devOpsAccessSetting);
                var result = await buildManagement.GetLatestBuildsAsync(BuildExtension.MasterBranchReporting, "refs/heads/master").ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
