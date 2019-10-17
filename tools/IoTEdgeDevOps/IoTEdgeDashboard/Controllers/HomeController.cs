// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Controllers
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using IoTEdgeDashboard.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    public class HomeController : Controller
    {
        readonly AppConfig _appConfig;

        public HomeController(IOptions<AppConfig> appConfigAccessor)
        {
            this._appConfig = appConfigAccessor.Value;
        }

        public async Task<IActionResult> Index()
        {
            var agentManagement = new AgentManagement("msazure", this._appConfig.PersonalAccessToken);
            IList<VstsAgent> agents = await agentManagement.GetAgentsAsync(AgentManagement.IoTEdgeAgentPoolId).ConfigureAwait(false);

            var agentMatrix = new AgentMatrix();
            agentMatrix.Update(agents.Select(IoTEdgeVstsAgent.Create).ToHashSet());

            var viewModel = new DashboardViewModel { AgentTable = agentMatrix };
            List<IoTEdgeVstsAgent> unmatchedAgents = agentMatrix.GetUnmatchedAgents().ToList();

            // Image build
            viewModel.ImageBuild = new ImageBuildViewModel
            {
                Group = new AgentDemandSet("Build Images", new HashSet<AgentCapability> { new AgentCapability("build-Image", "true") }),
                Agents = new List<IoTEdgeVstsAgent>()
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
