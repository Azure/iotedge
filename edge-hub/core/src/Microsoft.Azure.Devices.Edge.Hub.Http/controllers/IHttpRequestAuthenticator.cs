// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Util;

    public struct HttpAuthResult
    {
        public HttpAuthResult(bool authenticated, string errorMsg)
        {
            this.Authenticated = authenticated;
            this.ErrorMessage = Preconditions.CheckNotNull(errorMsg);
        }

        public bool Authenticated { get; }
        public string ErrorMessage { get; }
    }

    public interface IHttpRequestAuthenticator
    {
        Task<HttpAuthResult> AuthenticateAsync(string deviceId, Option<string> moduleId, Option<string> authChain, HttpContext context);
    }
}
