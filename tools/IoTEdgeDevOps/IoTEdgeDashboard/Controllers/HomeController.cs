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
            string masterBranch = "refs/heads/master";
            var buildManagement = new BuildManagement(new DevOpsAccessSetting(this._appConfig.PersonalAccessToken));
            IList<VstsBuild> builds = await buildManagement.GetLatestBuildsAsync(BuildExtension.MasterBranchReporting, masterBranch).ConfigureAwait(false);

            return this.View(
                new DashboardViewModel
                {
                    MasterBranch = new MasterBranch { Builds = builds }
                });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
