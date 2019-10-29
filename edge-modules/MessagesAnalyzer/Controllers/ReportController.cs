// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using System.Text;
    using System;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {

        AzureLogAnalytics logAnalytics = null;

        // GET api/report
        [HttpGet]
        public ActionResult<string> Get()
        {
            
            if(this.logAnalytics == null)
            {
                // BEARWASHERE
                //( Workspace Id, Key [Advance settings > connected sources], log type, api version  )
                this.logAnalytics = new AzureLogAnalytics(
                    Settings.Current.LogAnalyticWorkspaceId, 
                    Settings.Current.LogAnalyticSharedKey, 
                    Settings.Current.LogAnalyticLogType);
            }

            string resultJson = Reporter.GetReceivedMessagesReport(Settings.Current.ToleranceInMilliseconds).ToString();
            this.logAnalytics.Post(Encoding.UTF8.GetBytes(resultJson));
            return resultJson;
        }
    }
}
