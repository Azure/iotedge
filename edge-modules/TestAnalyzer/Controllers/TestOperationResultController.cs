// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
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
            LegacyTestOperationResult legacyResult = LegacyTestOperationResult.Convert(result);

            switch (result.ResultType)
            {
                case TestResultType.LegacyDirectMethod:
                    await ReportingCache.Instance.AddDirectMethodStatusAsync(legacyResult);
                    break;
                case TestResultType.LegacyTwin:
                    await ReportingCache.Instance.AddTwinStatusAsync(legacyResult);
                    break;
                default:
                    throw new InvalidDataException($"result source should be either 'LegacyDirectMethod' or 'LegacyTwin'. Current is '{result.Source}'.");
            }
            
            return this.StatusCode((int)HttpStatusCode.NoContent);
        }
    }
}
