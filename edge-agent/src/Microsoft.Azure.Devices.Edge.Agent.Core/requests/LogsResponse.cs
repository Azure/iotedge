// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class LogsResponse
    {
        public LogsResponse(string id, byte[] payload)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Payload = Preconditions.CheckNotNull(payload, nameof(payload));
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("payload")]
        public byte[] Payload { get; }
    }
}
