// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class TwinStatusController : Controller
    {
        // POST api/twinstatus
        [HttpPost]
        public async Task<ActionResult<bool>> Post(ResponseStatus twinStatus)
        {
            await CacheWithStorage.Instance.AddTwin(twinStatus);
            return true;
        }
    }
}
