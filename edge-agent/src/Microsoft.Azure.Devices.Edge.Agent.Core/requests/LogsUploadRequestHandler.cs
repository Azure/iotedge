// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, object>
    {
        readonly ILogsUploader logsUploader;
        readonly ILogsProvider logsProvider;
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public LogsUploadRequestHandler(ILogsUploader logsUploader, ILogsProvider logsProvider, IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.logsUploader = Preconditions.CheckNotNull(logsUploader, nameof(logsUploader));
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public override string RequestName => "UploadLogs";

        protected override async Task<Option<object>> HandleRequestInternal(Option<LogsUploadRequest> payloadOption, CancellationToken cancellationToken)
        {
            LogsUploadRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));

            IList<(string id, ModuleLogOptions logOptions)> logOptionsList = await payload.Items.MapToLogOptions(this.runtimeInfoProvider, payload.Encoding, payload.ContentType, cancellationToken);
            IEnumerable<Task> uploadLogsTasks = logOptionsList.Select(l => this.UploadLogs(payload.SasUrl, l.id, l.logOptions, cancellationToken));
            await Task.WhenAll(uploadLogsTasks);
            return Option.None<object>();
        }

        async Task UploadLogs(string sasUrl, string id, ModuleLogOptions moduleLogOptions, CancellationToken token)
        {
            if (moduleLogOptions.ContentType == LogsContentType.Json)
            {
                byte[] logBytes = await this.logsProvider.GetLogs(id, moduleLogOptions, token);
                await this.logsUploader.Upload(sasUrl, id, logBytes, moduleLogOptions.ContentEncoding, moduleLogOptions.ContentType);
            }
            else if (moduleLogOptions.ContentType == LogsContentType.Text)
            {
                Func<ArraySegment<byte>, Task> uploaderCallback = await this.logsUploader.GetUploaderCallback(sasUrl, id, moduleLogOptions.ContentEncoding, moduleLogOptions.ContentType);
                await this.logsProvider.GetLogsStream(id, moduleLogOptions, uploaderCallback, token);
            }
        }
    }
}
