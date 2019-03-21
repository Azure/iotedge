// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, object>
    {
        readonly ILogsUploader logsUploader;
        readonly ILogsProvider logsProvider;

        public LogsUploadRequestHandler(ILogsUploader logsUploader, ILogsProvider logsProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.logsUploader = Preconditions.CheckNotNull(logsUploader, nameof(logsUploader));
        }

        public override string RequestName => "UploadLogs";

        protected override async Task<Option<object>> HandleRequestInternal(Option<LogsUploadRequest> payloadOption)
        {
            LogsUploadRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));
            var moduleLogOptions = new ModuleLogOptions(payload.Id, payload.Encoding, payload.ContentType);
            byte[] logBytes = await this.logsProvider.GetLogs(moduleLogOptions, CancellationToken.None);
            await this.logsUploader.Upload(payload.SasUrl, payload.Id, logBytes, payload.Encoding, payload.ContentType);
            return Option.None<object>();
        }
    }
}
