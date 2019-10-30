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
        AzureLogAnalytics logAnalytics = null;

        // GET api/report
        [HttpGet]
        public ActionResult<string> Get()
        {
            if (Settings.Current.LogAnalyticEnabled && this.logAnalytics == null)
            {
                this.logAnalytics = new AzureLogAnalytics(
                    Settings.Current.LogAnalyticWorkspaceId,
                    Settings.Current.LogAnalyticSharedKey,
                    Settings.Current.LogAnalyticLogType);
            }

            string resultJson = Reporter.GetReceivedMessagesReport(Settings.Current.ToleranceInMilliseconds).ToString();
            if (Settings.Current.LogAnalyticEnabled)
            {
                this.logAnalytics.Post(Encoding.UTF8.GetBytes(resultJson));
            }

            return resultJson;
        }
    }
}
