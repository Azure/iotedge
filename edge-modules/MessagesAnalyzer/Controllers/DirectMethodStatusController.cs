// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class DirectMethodStatusController : Controller
    {
        // POST api/directmethodstatus
        [HttpPost]
        public async Task<ActionResult<bool>> Post(ResponseStatus methodCallStatus)
        {
            await MessagesCacheWithStorage.Instance.AddDirectMethod(methodCallStatus);
            return true;
        }
    }
}
