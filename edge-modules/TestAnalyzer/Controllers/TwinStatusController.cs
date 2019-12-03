// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class TwinStatusController : Controller
    {
        // POST api/twinstatus
        [HttpPost]
        public async Task<StatusCodeResult> PostAsync(CloudOperationStatus twinStatus)
        {
            await ReportingCache.Instance.AddTwinStatusAsync(twinStatus);
            return this.StatusCode((int)HttpStatusCode.NoContent);
        }
    }
}
