// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public struct HttpAuthResult
    {
        public HttpAuthResult(bool authenticated, string errorMsg)
        {
            this.Authenticated = authenticated;
            this.ErrorMsg = Preconditions.CheckNotNull(errorMsg);
        }

        public bool Authenticated { get; }
        public string ErrorMsg { get; }
    }

    public interface IHttpRequestAuthenticator
    {
        Task<HttpAuthResult> AuthenticateRequest(string deviceId, Option<string> moduleId, HttpContext context);
    }
}
