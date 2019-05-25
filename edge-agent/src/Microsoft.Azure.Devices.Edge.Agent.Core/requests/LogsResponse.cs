// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class LogsResponse
    {
        public LogsResponse(string id, byte[] payload)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.PayloadBytes = Option.Some(Preconditions.CheckNotNull(payload, nameof(payload)));
            this.Payload = Option.None<string>();
        }

        public LogsResponse(string id, string payload)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Payload = Option.Some(Preconditions.CheckNotNull(payload, nameof(payload)));
            this.PayloadBytes = Option.None<byte[]>();
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
