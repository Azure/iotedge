// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
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
        public int Status { get; }

        [JsonProperty("payload")]
        public JRaw Payload { get; }
    }

    public class MethodErrorResult : MethodResult
    {
        public MethodErrorResult(int status, JRaw payload, string message, string exceptionMessage)
            : base(status, payload)
        {
            this.Message = message;
            this.ExceptionMessage = exceptionMessage;
        }

        public string Message { get; }

        public string ExceptionMessage { get; }
    }
}
