// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class LogsStreamRequest
    {
        public LogsStreamRequest(string schemaVersion, List<LogRequestItem> items, LogsContentEncoding encoding, LogsContentType contentType)
        {
            this.SchemaVersion = schemaVersion;
            this.Items = Preconditions.CheckNotNull(items, nameof(items));
            this.Encoding = encoding;
            this.ContentType = contentType;
        }

        [JsonConstructor]
        LogsStreamRequest(string schemaVersion, List<LogRequestItem> items, LogsContentEncoding? encoding, LogsContentType? contentType)
            : this(schemaVersion, items, encoding ?? LogsContentEncoding.None, contentType ?? LogsContentType.Text)
        {
        }

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; }

        [JsonProperty("items")]
        [JsonConverter(typeof(SingleOrArrayConverter<LogRequestItem>))]
        public List<LogRequestItem> Items { get; }

        [JsonProperty("encoding", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(LogsContentEncoding.None)]
        public LogsContentEncoding Encoding { get; }

        [JsonProperty("contentType", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(LogsContentType.Text)]
        public LogsContentType ContentType { get; }
    }
}
