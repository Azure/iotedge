// Copyright (c) Microsoft. All rights reserved.
namespace IoTEdgeDashboard.Controllers
{
    using System.Diagnostics;
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

        public async Task<IActionResult> Index([FromQuery(Name = "branch")] string branch = "master")
        {
            var buildManagement = new BuildManagement(new DevOpsAccessSetting(this._appConfig.PersonalAccessToken));
            VstsBuild ciBuild = await buildManagement.GetLatestBuildAsync(BuildDefinitionIds.CI, branch).ConfigureAwait(false);

            return this.View(
                new DashboardViewModel
                {
                    CIBuild = ciBuild
                });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
