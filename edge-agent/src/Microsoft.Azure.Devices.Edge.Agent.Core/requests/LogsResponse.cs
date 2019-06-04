// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class LogsResponse
    {
        public LogsResponse(string id, byte[] payloadBytes)
            : this(id, null, payloadBytes)
        {
        }

        public LogsResponse(string id, string payload)
            : this(id, payload, null)
        {
        }

        [JsonConstructor]
        LogsResponse(string id, string payload, byte[] payloadBytes)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.PayloadBytes = Option.Maybe(payloadBytes);
            this.Payload = Option.Maybe(payload);
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("payloadBytes")]
        [JsonConverter(typeof(OptionConverter<byte[]>))]
        public Option<byte[]> PayloadBytes { get; }

        [JsonProperty("payload")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> Payload { get; }
    }
}
