// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using Newtonsoft.Json;

    public class MethodResult
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }
    }
}