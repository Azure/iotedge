// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Controllers
{
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
            await TestOperationResultStorage.Instance.AddTestOperationResultAsync(result);
            return this.StatusCode((int)HttpStatusCode.NoContent);
        }
    }
}
