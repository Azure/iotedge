// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class LogsStreamRequest
    {
        public LogsStreamRequest(string schemaVersion, string id, LogsContentEncoding encoding, LogsContentType contentType, ModuleLogFilter filter)
        {
            this.SchemaVersion = schemaVersion;
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Filter = filter ?? ModuleLogFilter.Empty;
            this.Encoding = encoding;
            this.ContentType = contentType;
        }

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("encoding", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(LogsContentEncoding.None)]
        public LogsContentEncoding Encoding { get; }

        [JsonProperty("contentType", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(LogsContentType.Text)]
        public LogsContentType ContentType { get; }

        [JsonProperty("filter")]
        public ModuleLogFilter Filter { get; }
    }
}
