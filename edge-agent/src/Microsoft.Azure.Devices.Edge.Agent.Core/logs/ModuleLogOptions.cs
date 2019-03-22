// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleLogOptions
    {
        public ModuleLogOptions(string id, LogsContentEncoding contentEncoding, LogsContentType contentType, ModuleLogFilter filter)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.ContentEncoding = contentEncoding;
            this.ContentType = contentType;
            this.Filter = Preconditions.CheckNotNull(filter, nameof(filter));
        }

        public string Id { get; }
        public LogsContentEncoding ContentEncoding { get; }
        public LogsContentType ContentType { get; }
        public ModuleLogFilter Filter { get; }
    }
}
