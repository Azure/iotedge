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
            try
            {
                var agentManagement = new AgentManagement("msazure", "<PAT>");
                IList<VstsAgent> agents = await agentManagement.GetAgentsAsync(AgentManagement.IoTEdgeAgentPoolId).ConfigureAwait(false);

                var agentMatrix = new AgentMatrix();
                agentMatrix.Update(agents.Select(IoTEdgeVstsAgent.Create).ToHashSet());
                var unmatchedAgents = agentMatrix.GetUnmatchedAgents();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
