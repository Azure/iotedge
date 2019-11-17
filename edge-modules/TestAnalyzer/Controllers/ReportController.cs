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
            if (Settings.Current.LogAnalyticEnabled)
            {
                this.PublishToLogAnalytics(deviceAnalysis);
            }

            return deviceAnalysis.ToString();
        }

        // GET api/report (exposed for backwards compatibility for snitcher)
        [HttpGet]
        public ActionResult<string> GetMessages()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticEnabled)
            {
                this.PublishToLogAnalytics(deviceAnalysis);
            }

            return JsonConvert.SerializeObject(deviceAnalysis.MessagesReport, Formatting.Indented);
        }

        private void PublishToLogAnalytics(DeviceAnalysis deviceAnalysis)
        {
            string messagesJson = JsonConvert.SerializeObject(deviceAnalysis.MessagesReport, Formatting.Indented);
            string twinsJson = JsonConvert.SerializeObject(deviceAnalysis.TwinsReport, Formatting.Indented);
            string directMethodsJson = JsonConvert.SerializeObject(deviceAnalysis.DmReport, Formatting.Indented);

            string workspaceId = Settings.Current.LogAnalyticWorkspaceId;
            string sharedKey = Settings.Current.LogAnalyticSharedKey;
            string logType = Settings.Current.LogAnalyticLogType;

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
