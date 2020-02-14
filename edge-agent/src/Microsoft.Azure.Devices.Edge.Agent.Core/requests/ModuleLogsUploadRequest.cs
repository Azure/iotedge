// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ModuleLogsUploadRequest
    {
        public ModuleLogsUploadRequest(
            string schemaVersion,
            List<LogRequestItem> items,
            LogsContentEncoding encoding,
            LogsContentType contentType,
            string sasUrl)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Encoding = encoding;
            this.ContentType = contentType;
            this.SasUrl = Preconditions.CheckNotNull(sasUrl, nameof(sasUrl));
            this.Items = Preconditions.CheckNotNull(items, nameof(items));
        }

        [JsonConstructor]
        ModuleLogsUploadRequest(
            string schemaVersion,
            List<LogRequestItem> items,
            LogsContentEncoding? encoding,
            LogsContentType? contentType,
            string sasUrl)
            : this(schemaVersion, items, encoding ?? LogsContentEncoding.None, contentType ?? LogsContentType.Json, sasUrl)
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

        [JsonIgnore]
        public string SasUrl { get; }
    }
}
