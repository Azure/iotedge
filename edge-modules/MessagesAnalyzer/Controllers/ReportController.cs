// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using System.Text;
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
            AzureLogAnalytics logAnalytics = null;

            if (Settings.Current.LogAnalyticEnabled)
            {
                logAnalytics = AzureLogAnalytics.initInstance(
                    Settings.Current.LogAnalyticWorkspaceId,
                    Settings.Current.LogAnalyticSharedKey );

                // Upload the data to Log Analytics
                logAnalytics.Post(Encoding.UTF8.GetBytes(resultJson), Settings.Current.LogAnalyticLogType);
                //logAnalytics.PostAsync(resultJson, Settings.Current.LogAnalyticLogType);
            }

            return resultJson;
        }
    }
}
