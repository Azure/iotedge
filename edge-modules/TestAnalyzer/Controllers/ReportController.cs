// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("Analyzer");

        // GET api/report/all
        [HttpGet("all")]
        public async Task<ActionResult<string>> GetReport()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticsEnabled)
            {
                await this.PublishToLogAnalytics(deviceAnalysis);
            }

            return deviceAnalysis.ToString();
        }

        // GET api/report (exposed for backwards compatibility for snitcher which will eventually be deprecated)
        [HttpGet]
        public async Task<ActionResult<string>> GetMessages()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticsEnabled)
            {
                await this.PublishToLogAnalytics(deviceAnalysis); // TODO: await
            }

            return deviceAnalysis.MessagesReport.ToString();
        }

        private async Task PublishToLogAnalytics(DeviceAnalysis deviceAnalysis)
        {
            string messagesJson = JsonConvert.SerializeObject(deviceAnalysis.MessagesReport, Formatting.Indented);
            string twinsJson = JsonConvert.SerializeObject(deviceAnalysis.TwinsReport, Formatting.Indented);
            string directMethodsJson = JsonConvert.SerializeObject(deviceAnalysis.DmReport, Formatting.Indented);

            string workspaceId = Settings.Current.LogAnalyticsWorkspaceId;
            string sharedKey = Settings.Current.LogAnalyticsSharedKey;
            string logType = Settings.Current.LogAnalyticsLogType;

            try
            {
                // Upload the data to Log Analytics for our dashboards
                await AzureLogAnalytics.Instance.PostAsync(
                    workspaceId,
                    sharedKey,
                    messagesJson,
                    logType);

                await AzureLogAnalytics.Instance.PostAsync(
                    workspaceId,
                    sharedKey,
                    twinsJson,
                    logType);

                await AzureLogAnalytics.Instance.PostAsync(
                    workspaceId,
                    sharedKey,
                    directMethodsJson,
                    logType);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed uploading reports to log analytics: {e}");
            }
        }
    }
}
