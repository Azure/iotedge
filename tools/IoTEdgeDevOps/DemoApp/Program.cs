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
                var agentManagement = new AgentManagement(devOpsAccessSetting);
                IList<VstsAgent> agents = await agentManagement.GetAgentsAsync(AgentManagement.IoTEdgeAgentPoolId).ConfigureAwait(false);

                var agentMatrix = new AgentMatrix();
                agentMatrix.Update(agents.Select(IoTEdgeVstsAgent.Create).ToHashSet());
                var unmatchedAgents = agentMatrix.GetUnmatchedAgents();

                var buildManagement = new BuildManagement(devOpsAccessSetting);
                var result1 = await buildManagement.GetLatestBuildAsync(BuildDefinitionIds.CI, "master").ConfigureAwait(false);
                var result2 = await buildManagement.GetLatestBuildAsync(BuildDefinitionIds.CI, "release/1.0.9").ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
