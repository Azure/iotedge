// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        // GET api/report/all
        [HttpGet("all")]
        public ActionResult<string> GetReport()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticsEnabled)
            {
                this.PublishToLogAnalytics(deviceAnalysis);
            }

            return deviceAnalysis.ToString();
        }

        // GET api/report (exposed for backwards compatibility for snitcher which will eventually be deprecated)
        [HttpGet]
        public ActionResult<string> GetMessages()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticsEnabled)
            {
                this.PublishToLogAnalytics(deviceAnalysis);
            }

            return deviceAnalysis.MessagesReport.ToString();
        }

        private void PublishToLogAnalytics(DeviceAnalysis deviceAnalysis)
        {
            string messagesJson = JsonConvert.SerializeObject(deviceAnalysis.MessagesReport, Formatting.Indented);
            string twinsJson = JsonConvert.SerializeObject(deviceAnalysis.TwinsReport, Formatting.Indented);
            string directMethodsJson = JsonConvert.SerializeObject(deviceAnalysis.DmReport, Formatting.Indented);

            string workspaceId = Settings.Current.LogAnalyticsWorkspaceId;
            string sharedKey = Settings.Current.LogAnalyticsSharedKey;
            string logType = Settings.Current.LogAnalyticsLogType;

            // Upload the data to Log Analytics for our dashboards
            AzureLogAnalytics.Instance.PostAsync(
                workspaceId,
                sharedKey,
                messagesJson,
                logType);

            AzureLogAnalytics.Instance.PostAsync(
                workspaceId,
                sharedKey,
                twinsJson,
                logType);

            AzureLogAnalytics.Instance.PostAsync(
                workspaceId,
                sharedKey,
                directMethodsJson,
                logType);
        }
    }
}
