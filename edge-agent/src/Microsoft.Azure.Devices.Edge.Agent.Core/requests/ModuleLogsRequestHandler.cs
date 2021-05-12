// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics.Concurrency;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ModuleLogsRequestHandler : RequestHandlerBase<ModuleLogsRequest, IEnumerable<ModuleLogsResponse>>
    {
        // Max size is 128 KB, leave 1KB buffer
        const int MaxPayloadSize = 127000;
        const string Name = "GetModuleLogs";

        static readonly Version ExpectedSchemaVersion = new Version("1.0");

        readonly ILogsProvider logsProvider;
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public ModuleLogsRequestHandler(ILogsProvider logsProvider, IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public override string RequestName => Name;

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
            int messageSize = 0;
            IEnumerable<Task<ModuleLogsResponse>> uploadLogsTasks = logOptionsList.Select(
                async l =>
                {
                    byte[] moduleLogs = await this.logsProvider.GetLogs(l.id, l.logOptions, cancellationToken);

                    Events.ReceivedModuleLogs(moduleLogs, l.id);

                    if (l.logOptions.ContentEncoding == LogsContentEncoding.Gzip)
                    {
                        Interlocked.Add(ref messageSize, moduleLogs.Length);
                        return new ModuleLogsResponse(l.id, moduleLogs);
                    }
                    else
                    {
                        string encodedLogs = moduleLogs.FromBytes();
                        Interlocked.Add(ref messageSize, encodedLogs.Length);

                        return new ModuleLogsResponse(l.id, encodedLogs);
                    }
                });

            IEnumerable<ModuleLogsResponse> response = await Task.WhenAll(uploadLogsTasks);

            if (messageSize > MaxPayloadSize)
            {
                string message = Events.LargePayload(messageSize, logOptionsList.Select(o => o.logOptions));
                throw new ArgumentException(message);
            }

            return Option.Some(response);
        }

        static class Events
        {
            const int IdStart = AgentEventIds.LogsRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleLogsRequestHandler>();

            enum EventIds
            {
                ReceivedModuleLogs = IdStart + 1,
                LargePayload,
                ProcessingRequest,
                MismatchedMinorVersions,
            }

            public static void ReceivedModuleLogs(byte[] moduleLogs, string id)
            {
                Log.LogInformation((int)EventIds.ReceivedModuleLogs, $"Received {moduleLogs.Length} bytes of logs for {id}");
            }

            public static void ProcessingRequest(ModuleLogsRequest payload)
            {
                Log.LogInformation((int)EventIds.ProcessingRequest, $"Processing request to get logs for {payload.ToJson()}");
            }

            public static void MismatchedMinorVersions(string payloadSchemaVersion, Version expectedSchemaVersion)
            {
                Log.LogWarning((int)EventIds.MismatchedMinorVersions, $"Logs upload request schema version {payloadSchemaVersion} does not match expected schema version {expectedSchemaVersion}. Some settings may not be supported.");
            }

            public static string LargePayload(int size, IEnumerable<ModuleLogOptions> options)
            {
                // TODO: make/get aka link for documentation
                string message = $"The payload is too large for a direct method. {Name} supports up to {MaxPayloadSize} bytes of logs. The current request returned {size} bytes.\nTry reducing the size of the logs by setting the 'tail', 'since' and 'from' fields in log options filter. For more information, see https://aka.ms/iotedge-log-pull \nCurrent options settings are:\n{Newtonsoft.Json.JsonConvert.SerializeObject(options, Newtonsoft.Json.Formatting.Indented)}";

                Log.LogWarning((int)EventIds.LargePayload, message);

                return message;
            }
        }
    }
}
