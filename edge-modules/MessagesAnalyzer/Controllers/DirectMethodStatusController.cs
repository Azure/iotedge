// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using System;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class DirectMethodStatusController : Controller
    {
        // POST api/directmethodstatus
        [HttpPost]
        public ActionResult<bool> Post(DirectMethodStatus methodCallStatus)
        {
            MessagesCache.Instance.AddDirectMethodStatus(methodCallStatus);
            return true;
        }
    }
}
