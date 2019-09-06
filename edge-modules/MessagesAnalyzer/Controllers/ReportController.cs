// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        // GET api/report
        [HttpGet]
        public ActionResult<string> Get()
        {
            return Reporter.GetDeviceReport(Settings.Current.ToleranceInMilliseconds).ToString();
        }
    }
}
