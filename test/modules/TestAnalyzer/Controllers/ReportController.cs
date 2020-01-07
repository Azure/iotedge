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
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(ReportController));

        // GET api/report/all
        [HttpGet("all")]
        public async Task<ContentResult> GetReport()
        {
            TestResultAnalysis deviceAnalysis = TestResultReporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticsEnabled)
            {
                await this.PublishToLogAnalyticsAsync(deviceAnalysis);
            }

            return new ContentResult { Content = deviceAnalysis.ToString() };
        }

        // GET api/report (exposed for backwards compatibility for snitcher which will eventually be deprecated)
        [HttpGet]
        public async Task<ContentResult> GetMessagesAsync()
        {
            TestResultAnalysis deviceAnalysis = TestResultReporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds);
            if (Settings.Current.LogAnalyticsEnabled)
            {
                await this.PublishToLogAnalyticsAsync(deviceAnalysis);
            }

            return new ContentResult { Content = JsonConvert.SerializeObject(deviceAnalysis.MessagesReport, Formatting.Indented) }; // explicit serialization needed due to the wrapping list
        }

        async Task PublishToLogAnalyticsAsync(TestResultAnalysis deviceAnalysis)
        {
            string messagesJson = JsonConvert.SerializeObject(deviceAnalysis.MessagesReport, Formatting.Indented);
            string twinsJson = JsonConvert.SerializeObject(deviceAnalysis.TwinsReport, Formatting.Indented);
            string directMethodsJson = JsonConvert.SerializeObject(deviceAnalysis.DmReport, Formatting.Indented);

            string workspaceId = Settings.Current.LogAnalyticsWorkspaceId;
            string sharedKey = Settings.Current.LogAnalyticsSharedKey;
            string logType = Settings.Current.LogAnalyticsLogType;

            // Upload the data to Log Analytics for our dashboards
            try
            {
                Task reportMessages = AzureLogAnalytics.Instance.PostAsync(
                    workspaceId,
                    sharedKey,
                    messagesJson,
                    logType);

                Task reportTwins = AzureLogAnalytics.Instance.PostAsync(
                    workspaceId,
                    sharedKey,
                    twinsJson,
                    logType);

                Task reportDirectMethods = AzureLogAnalytics.Instance.PostAsync(
                    workspaceId,
                    sharedKey,
                    directMethodsJson,
                    logType);

                await Task.WhenAll(reportMessages, reportTwins, reportDirectMethods);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed uploading reports to log analytics:", e);
            }
        }
    }
}
