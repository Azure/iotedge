// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Controllers
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;
    using TestOperationResult = TestResultCoordinator.TestOperationResult;

    [Route("api/[controller]")]
    [ApiController]
    public class TestOperationResultController : Controller
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TestOperationResultController));

        // POST api/TestOperationResult
        [HttpPost]
        public async Task<StatusCodeResult> PostAsync(TestOperationResult result)
        {
            try
            {
                bool success = await TestOperationResultStorage.AddResultAsync(result);
                Logger.LogDebug($"Received test result: {result.Source}, {result.Type}, {success}");
                return success ? this.StatusCode((int)HttpStatusCode.NoContent) : this.StatusCode((int)HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return this.StatusCode((int)HttpStatusCode.InternalServerError);
            }
        }
    }
}
