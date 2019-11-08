// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
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
            string resultJson = Reporter.GetReceivedMessagesReport(Settings.Current.ToleranceInMilliseconds).ToString();

            if (Settings.Current.LogAnalyticEnabled)
            {
                AzureLogAnalytics logAnalytics = AzureLogAnalytics.InitInstance();

                // Upload the data to Log Analytics
                logAnalytics.PostAsync(Settings.Current.LogAnalyticWorkspaceId,
                    Settings.Current.LogAnalyticSharedKey,
                    resultJson,
                    Settings.Current.LogAnalyticLogType);
            }

            return resultJson;
        }
    }
}
