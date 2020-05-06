// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using IoTEdgeDashboard.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    public class AgentController : Controller
    {
        readonly AppConfig _appConfig;

        public AgentController(IOptions<AppConfig> appConfigAccessor)
        {
            this._appConfig = appConfigAccessor.Value;
        }

        public async Task<IActionResult> Index()
        {
            var agentManagement = new AgentManagement(new DevOpsAccessSetting(this._appConfig.PersonalAccessToken));
            IList<VstsAgent> agents = await agentManagement.GetAgentsAsync(AgentManagement.IoTEdgeAgentPoolId).ConfigureAwait(false);

            var agentMatrix = new AgentMatrix();
            agentMatrix.Update(agents.Select(IoTEdgeAgent.Create).ToHashSet());

            var viewModel = new AgentMatrixViewModel { AgentTable = agentMatrix };
            List<IoTEdgeAgent> unmatchedAgents = agentMatrix.GetUnmatchedAgents().ToList();

            // Image build
            viewModel.ImageBuild = new ImageBuildViewModel
            {
                Group = new AgentDemandSet("Build Images", new HashSet<AgentCapability> { new AgentCapability("build-Image", "true") }),
                Agents = new List<IoTEdgeAgent>()
            };

            for (int i = unmatchedAgents.Count - 1; i >= 0; i--)
            {
                if (unmatchedAgents[i].Match(viewModel.ImageBuild.Group.Capabilities))
                {
                    viewModel.ImageBuild.Agents.Add(unmatchedAgents[i]);
                    unmatchedAgents.RemoveAt(i);
                }
            }

            viewModel.UnmatchedAgents = unmatchedAgents;

            return this.View(viewModel);
        }
    }
}
