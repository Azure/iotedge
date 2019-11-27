// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class DirectMethodStatusController : Controller
    {
        // POST api/directmethodstatus
        [HttpPost]
        public async Task<EmptyResult> Post(ResponseStatus methodCallStatus)
        {
            await ReportingCacheWithStorage.Instance.AddDirectMethod(methodCallStatus);
            return new EmptyResult();
        }
    }
}
