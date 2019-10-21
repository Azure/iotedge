// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using System;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class TwinStatusController : Controller
    {
        // POST api/directmethodstatus
        [HttpPost]
        public ActionResult<bool> Post(ResponseStatus twinStatus)
        {
            MessagesCache.Instance.AddTwinStatus(twinStatus);
            return true;
        }
    }
}
