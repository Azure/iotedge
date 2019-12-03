// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class DirectMethodStatusController : Controller
    {
        // POST api/directmethodstatus
        [HttpPost]
        public async Task<StatusCodeResult> PostAsync(CloudOperationStatus methodCallStatus)
        {
            await ReportingCache.Instance.AddDirectMethodStatusAsync(methodCallStatus);
            return this.StatusCode((int)HttpStatusCode.NoContent);
        }
    }
}
