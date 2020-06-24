// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public abstract class MethodResult
    {
        protected MethodResult(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        [JsonIgnore]
        public HttpStatusCode StatusCode { get; }
    }

    public class MethodSuccessResult : MethodResult
    {
        public MethodSuccessResult(int status, JRaw payload)
            : base(HttpStatusCode.OK)
        {
            this.Status = status;
            this.Payload = payload;
        }

        [JsonProperty("status")]
        public int Status { get; }

        [JsonProperty("payload")]
        public JRaw Payload { get; }
    }

    public class MethodErrorResult : MethodResult
    {
        public MethodErrorResult(HttpStatusCode statusCode, string message)
            : base(statusCode)
        {
            this.Message = message;
        }

        [JsonProperty("message")]
        public string Message { get; }
    }
}
