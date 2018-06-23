// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class MethodResult
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("payload")]
        public JRaw Payload { get; set; }
    }
}
