// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class TwinStatusController : Controller
    {
        // POST api/twinstatus
        [HttpPost]
        public async Task<EmptyResult> Post(ResponseStatus twinStatus)
        {
            await ReportingCacheWithStorage.Instance.AddTwin(twinStatus);
            return new EmptyResult();
        }
    }
}
