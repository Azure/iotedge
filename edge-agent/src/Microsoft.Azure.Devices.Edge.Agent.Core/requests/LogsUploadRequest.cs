// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequest
    {
        public LogsUploadRequest(string id, LogsContentEncoding encoding, LogsContentType contentType, string sasUrl)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Encoding = encoding;
            this.ContentType = contentType;
            this.SasUrl = sasUrl;
        }

        LogsUploadRequest(string id, LogsContentEncoding? encoding, LogsContentType? contentType, string sasUrl)
            : this(id, encoding.HasValue ? encoding.Value : LogsContentEncoding.None, contentType.HasValue ? contentType.Value : LogsContentType.Json, sasUrl)
        {
        }

        public string Id { get; }
        public LogsContentEncoding Encoding { get; }
        public LogsContentType ContentType { get; }
        public string SasUrl { get; }
    }
}
