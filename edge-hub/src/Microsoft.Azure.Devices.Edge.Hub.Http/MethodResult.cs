// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class MethodResult
    {
        public MethodResult(int status, JRaw payload)
        {
            this.Status = status;
            this.Payload = payload;
        }

        [JsonProperty("status")]
        public virtual int Status { get; }

        [JsonProperty("payload")]
        public virtual JRaw Payload { get; }
    }

    public class MethodErrorResult : MethodResult
    {
        public MethodErrorResult(HttpStatusCode statusCode, string message)
            : base(0, null)
        {
            this.StatusCode = statusCode;
            this.Message = message;
        }

        [JsonIgnore]
        public override int Status { get; }

        [JsonIgnore]
        public override JRaw Payload { get; }

        [JsonIgnore]
        public HttpStatusCode StatusCode { get; }

        [JsonProperty("message")]
        public string Message { get; }
    }
}
