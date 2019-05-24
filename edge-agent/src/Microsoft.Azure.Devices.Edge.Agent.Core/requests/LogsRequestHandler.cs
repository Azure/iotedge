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

    public class LogsRequestHandler : RequestHandlerBase<LogsRequest, IEnumerable<LogsResponse>>
    {
        readonly ILogsProvider logsProvider;
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public LogsRequestHandler(ILogsProvider logsProvider, IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public override string RequestName => "GetLogs";

        protected override async Task<Option<IEnumerable<LogsResponse>>> HandleRequestInternal(Option<LogsRequest> payloadOption, CancellationToken cancellationToken)
        {
            LogsRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));

            ILogsRequestToOptionsMapper requestToOptionsMapper = new LogsRequestToOptionsMapper(
                this.runtimeInfoProvider,
                LogsContentEncoding.None,
                payload.ContentType,
                LogOutputFraming.None,
                Option.None<LogsOutputGroupingConfig>(),
                false);
            IList<(string id, ModuleLogOptions logOptions)> logOptionsList = await requestToOptionsMapper.MapToLogOptions(payload.Items, cancellationToken);
            IEnumerable<Task<LogsResponse>> uploadLogsTasks = logOptionsList.Select(async l =>
            {
                byte[] moduleLogs = await this.logsProvider.GetLogs(l.id, l.logOptions, cancellationToken);
                return new LogsResponse(l.id, moduleLogs);
            });
            IEnumerable<LogsResponse> response = await Task.WhenAll(uploadLogsTasks);
            return Option.Some(response);
        }
    }
}
