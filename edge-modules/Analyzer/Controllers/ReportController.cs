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
        public ActionResult<string> Get()
        {
            DeviceAnalysis deviceAnalysis = Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            string messagesJson = deviceAnalysis.MessagesReport.ToString();
            string twinsJson = deviceAnalysis.TwinsReport.ToString();
            string directMethodsJson = deviceAnalysis.DmReport.ToString();

            if (Settings.Current.LogAnalyticEnabled)
            {
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

            return deviceAnalysis.ToString();
        }
    }
}
