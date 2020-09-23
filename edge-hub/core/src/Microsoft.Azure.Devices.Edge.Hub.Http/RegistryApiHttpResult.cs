// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net;

    public class RegistryApiHttpResult
    {
        public RegistryApiHttpResult(HttpStatusCode statusCode, string jsonContent)
        {
            this.StatusCode = statusCode;
            this.JsonContent = jsonContent ?? string.Empty;
        }

        public HttpStatusCode StatusCode { get; }

        public string JsonContent { get; }
    }
}
