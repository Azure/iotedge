// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Controllers
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class TestOperationResultController : Controller
    {
        // POST api/TestOperationResult
        [HttpPost]
        public async Task<StatusCodeResult> PostAsync(TestOperationResult result)
        {
            try
            {
                bool success = await TestOperationResultStorage.AddResultAsync(result);
                return success ? this.StatusCode((int)HttpStatusCode.NoContent) : this.StatusCode((int)HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return this.StatusCode((int)HttpStatusCode.InternalServerError);
            }
        }
    }
}
