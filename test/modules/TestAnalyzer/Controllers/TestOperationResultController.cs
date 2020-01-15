// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;

    [Route("api/[controller]")]
    [ApiController]
    public class TestOperationResultController : Controller
    {
        // POST api/TestOperationResult
        [HttpPost]
        public async Task<StatusCodeResult> PostAsync(TestOperationResult result)
        {
            await ReportingCache.Instance.AddResultAsync(result);
            return this.StatusCode((int)HttpStatusCode.NoContent);
        }
    }
}
