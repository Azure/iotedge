// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer.Controllers
{
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        // GET api/report
        [HttpGet]
        public ActionResult<string> GetReport()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticEnabled)
            {
                this.PublishToLogAnalytics(deviceAnalysis);
            }

            return deviceAnalysis.ToString();
        }

        // GET api/report/messages (exposed for backwards compatibility for snitcher)
        [HttpGet("/messages")]
        public ActionResult<string> GetMessages()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticEnabled)
            {
                this.PublishToLogAnalytics(deviceAnalysis);
            }

            string messagesJson = deviceAnalysis.MessagesReport.ToString();
            return deviceAnalysis.ToString();
        }

        private void PublishToLogAnalytics(DeviceAnalysis deviceAnalysis)
        {
            string messagesJson = deviceAnalysis.MessagesReport.ToString();
            string twinsJson = deviceAnalysis.TwinsReport.ToString();
            string directMethodsJson = deviceAnalysis.DmReport.ToString();

            string workspaceId = Settings.Current.LogAnalyticWorkspaceId;
            string sharedKey = Settings.Current.LogAnalyticSharedKey;
            string logType = Settings.Current.LogAnalyticLogType;

            // Upload the data to Log Analytics
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
