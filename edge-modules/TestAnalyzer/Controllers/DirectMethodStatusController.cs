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
        public async Task<StatusCodeResult> PostAsync(ResponseStatus methodCallStatus)
        {
            await ReportingCacheWithStorage.Instance.AddDirectMethodAsync(methodCallStatus);
            return this.StatusCode((int)HttpStatusCode.OK);
        }
    }
}
