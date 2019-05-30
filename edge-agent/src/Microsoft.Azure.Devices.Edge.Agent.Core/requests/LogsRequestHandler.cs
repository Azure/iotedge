// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LogsRequestHandler : RequestHandlerBase<LogsRequest, IEnumerable<LogsResponse>>
    {
        const int MaxTailValue = 500;
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
                payload.Encoding,
                payload.ContentType,
                LogOutputFraming.None,
                Option.None<LogsOutputGroupingConfig>(),
                false);

            IList<(string id, ModuleLogOptions logOptions)> logOptionsList = await requestToOptionsMapper.MapToLogOptions(payload.Items, cancellationToken);
            IEnumerable<Task<LogsResponse>> uploadLogsTasks = logOptionsList.Select(
                async l =>
                {
                    Events.ReceivedLogOptions(l);
                    ModuleLogOptions logOptions = l.logOptions.Filter.Tail
                        .Filter(t => t < MaxTailValue)
                        .Map(t => l.logOptions)
                        .GetOrElse(
                            () =>
                            {
                                var filter = new ModuleLogFilter(Option.Some(MaxTailValue), l.logOptions.Filter.Since, l.logOptions.Filter.LogLevel, l.logOptions.Filter.RegexString);
                                return new ModuleLogOptions(l.logOptions.ContentEncoding, l.logOptions.ContentType, filter, l.logOptions.OutputFraming, l.logOptions.OutputGroupingConfig, l.logOptions.Follow);
                            });

                    byte[] moduleLogs = await this.logsProvider.GetLogs(l.id, logOptions, cancellationToken);

                    Events.ReceivedModuleLogs(moduleLogs, l.id);
                    return logOptions.ContentEncoding == LogsContentEncoding.Gzip
                        ? new LogsResponse(l.id, moduleLogs)
                        : new LogsResponse(l.id, moduleLogs.FromBytes());
                });
            IEnumerable<LogsResponse> response = await Task.WhenAll(uploadLogsTasks);
            return Option.Some(response);
        }

        static class Events
        {
            const int IdStart = AgentEventIds.LogsRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<LogsRequestHandler>();

            enum EventIds
            {
                ReceivedModuleLogs = IdStart + 1,
                ReceivedLogOptions
            }

            public static void ReceivedModuleLogs(byte[] moduleLogs, string id)
            {
                Log.LogInformation((int)EventIds.ReceivedModuleLogs, $"Received {moduleLogs.Length} bytes of logs for {id}");
            }

            public static void ReceivedLogOptions((string id, ModuleLogOptions logOptions) receivedLogOptions)
            {
                if (receivedLogOptions.logOptions.Filter.Tail.HasValue)
                {
                    receivedLogOptions.logOptions.Filter.Tail.ForEach(
                        t => Log.LogInformation(
                            (int)EventIds.ReceivedLogOptions,
                            t < MaxTailValue
                                ? $"Received log options for {receivedLogOptions.id} with tail value {t}"
                                : $"Received log options for {receivedLogOptions.id} with tail value {t} which is larger than the maximum supported value, setting tail value to {MaxTailValue}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.ReceivedLogOptions, $"Received log options for {receivedLogOptions.id} with no tail value specified, setting tail value to {MaxTailValue}");
                }
            }
        }
    }
}
