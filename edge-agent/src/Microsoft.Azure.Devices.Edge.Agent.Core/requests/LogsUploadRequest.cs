// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class LogsUploadRequest
    {
        public LogsUploadRequest(
            string id,
            LogsContentEncoding encoding,
            LogsContentType contentType,
            string sasUrl,
            ModuleLogFilter filter)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Encoding = encoding;
            this.ContentType = contentType;
            this.SasUrl = sasUrl;
            this.Filter = filter ?? ModuleLogFilter.Empty;
        }

        [JsonConstructor]
        LogsUploadRequest(
            string id,
            LogsContentEncoding? encoding,
            LogsContentType? contentType,
            string sasUrl,
            ModuleLogFilter filter)
            : this(id, encoding ?? LogsContentEncoding.None, contentType ?? LogsContentType.Json, sasUrl, filter)
        {
        }

        public string Id { get; }

        public LogsContentEncoding Encoding { get; }

        public LogsContentType ContentType { get; }

        public string SasUrl { get; }

        public ModuleLogFilter Filter { get; }
    }
}
