// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using System.Text;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {

        AzureLogAnalytics logAnalytics = null;
        ReportController()
        {
            if(this.logAnalytics == null)
            {
                // BEARWASHERE
                //( Workspace Id, Key [Advance settings > connected sources], log type, api version  )
                this.logAnalytics = new AzureLogAnalytics(
                    "be10913e-ac28-44d0-9534-207a6df99b0d", 
                    "Fft9PBDAyW68EAabfF5Gk1v1hSjtzLY+Fi0LJ9dncBdMRRc1h/xgxKh7jz3w9sztMPEL63berYS9QRHKCYvRew==", 
                    "promMetrics");
            }
        }

        // GET api/report
        [HttpGet]
        public ActionResult<string> Get()
        {
            string resultJson = Reporter.GetReceivedMessagesReport(Settings.Current.ToleranceInMilliseconds).ToString();
            this.logAnalytics.Post(Encoding.UTF8.GetBytes(resultJson));
            return resultJson;
            //return Reporter.GetReceivedMessagesReport(Settings.Current.ToleranceInMilliseconds).ToString();
        }
    }
}
