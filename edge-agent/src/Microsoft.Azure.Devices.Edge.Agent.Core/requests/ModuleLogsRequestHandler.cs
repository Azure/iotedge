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

    public class ModuleLogsRequestHandler : RequestHandlerBase<ModuleLogsRequest, IEnumerable<ModuleLogsResponse>>
    {
        const int MaxTailValue = 500;

        static readonly Version ExpectedSchemaVersion = new Version("1.0");

        readonly ILogsProvider logsProvider;
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public ModuleLogsRequestHandler(ILogsProvider logsProvider, IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public override string RequestName => "GetModuleLogs";

        protected override async Task<Option<IEnumerable<ModuleLogsResponse>>> HandleRequestInternal(Option<ModuleLogsRequest> payloadOption, CancellationToken cancellationToken)
        {
            ModuleLogsRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));
            if (ExpectedSchemaVersion.CompareMajorVersion(payload.SchemaVersion, "logs upload request schema") != 0)
            {
                Events.MismatchedMinorVersions(payload.SchemaVersion, ExpectedSchemaVersion);
            }

            Events.ProcessingRequest(payload);

            ILogsRequestToOptionsMapper requestToOptionsMapper = new LogsRequestToOptionsMapper(
                this.runtimeInfoProvider,
                payload.Encoding,
                payload.ContentType,
                LogOutputFraming.None,
                Option.None<LogsOutputGroupingConfig>(),
                false);

            IList<(string id, ModuleLogOptions logOptions)> logOptionsList = await requestToOptionsMapper.MapToLogOptions(payload.Items, cancellationToken);
            IEnumerable<Task<ModuleLogsResponse>> uploadLogsTasks = logOptionsList.Select(
                async l =>
                {
                    Events.ReceivedLogOptions(l);
                    ModuleLogOptions logOptions = l.logOptions.Filter.Tail
                        .Filter(t => t < MaxTailValue)
                        .Map(t => l.logOptions)
                        .GetOrElse(
                            () =>
                            {
                                var filter = new ModuleLogFilter(Option.Some(MaxTailValue), l.logOptions.Filter.Since, l.logOptions.Filter.Until, l.logOptions.Filter.LogLevel, l.logOptions.Filter.RegexString);
                                return new ModuleLogOptions(l.logOptions.ContentEncoding, l.logOptions.ContentType, filter, l.logOptions.OutputFraming, l.logOptions.OutputGroupingConfig, l.logOptions.Follow);
                            });

                    byte[] moduleLogs = await this.logsProvider.GetLogs(l.id, logOptions, cancellationToken);

                    Events.ReceivedModuleLogs(moduleLogs, l.id);
                    return logOptions.ContentEncoding == LogsContentEncoding.Gzip
                        ? new ModuleLogsResponse(l.id, moduleLogs)
                        : new ModuleLogsResponse(l.id, moduleLogs.FromBytes());
                });
            IEnumerable<ModuleLogsResponse> response = await Task.WhenAll(uploadLogsTasks);
            return Option.Some(response);
        }

        static class Events
        {
            const int IdStart = AgentEventIds.LogsRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleLogsRequestHandler>();

            enum EventIds
            {
                ReceivedModuleLogs = IdStart + 1,
                ReceivedLogOptions,
                ProcessingRequest,
                MismatchedMinorVersions
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

            public static void ProcessingRequest(ModuleLogsRequest payload)
            {
                Log.LogInformation((int)EventIds.ProcessingRequest, $"Processing request to get logs for {payload.ToJson()}");
            }

            public static void MismatchedMinorVersions(string payloadSchemaVersion, Version expectedSchemaVersion)
            {
                Log.LogWarning((int)EventIds.MismatchedMinorVersions, $"Logs upload request schema version {payloadSchemaVersion} does not match expected schema version {expectedSchemaVersion}. Some settings may not be supported.");
            }
        }
    }
}
