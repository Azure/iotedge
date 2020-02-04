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

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, TaskStatusResponse>
    {
        static readonly Version ExpectedSchemaVersion = new Version("1.0");

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

        protected override async Task<Option<TaskStatusResponse>> HandleRequestInternal(Option<LogsUploadRequest> payloadOption, CancellationToken cancellationToken)
        {
            LogsUploadRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));
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
                Option.Some(new LogsOutputGroupingConfig(100, TimeSpan.FromSeconds(10))),
                false);
            IList<(string id, ModuleLogOptions logOptions)> logOptionsList = await requestToOptionsMapper.MapToLogOptions(payload.Items, cancellationToken);
            IEnumerable<Task> uploadLogsTasks = logOptionsList.Select(l => this.UploadLogs(payload.SasUrl, l.id, l.logOptions, cancellationToken));
            (string correlationId, BackgroundTaskStatus status) = BackgroundTask.Run(
                () =>
                {
                    try
                    {
                        return Task.WhenAll(uploadLogsTasks);
                    }
                    catch (Exception e)
                    {
                        Events.ErrorUploadingLogs(e);
                        throw;
                    }
                },
                "upload logs",
                cancellationToken);
            return Option.Some(TaskStatusResponse.Create(correlationId, status));
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

            Events.UploadLogsFinished(id);
        }

        static class Events
        {
            const int IdStart = AgentEventIds.LogsUploadRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<LogsUploadRequestHandler>();

            enum EventIds
            {
                MismatchedMinorVersions = IdStart,
                ProcessingRequest,
                UploadLogsFinished,
                ErrorUploadingLogs
            }

            public static void MismatchedMinorVersions(string payloadSchemaVersion, Version expectedSchemaVersion)
            {
                Log.LogWarning((int)EventIds.MismatchedMinorVersions, $"Logs upload request schema version {payloadSchemaVersion} does not match expected schema version {expectedSchemaVersion}. Some settings may not be supported.");
            }

            public static void ProcessingRequest(LogsUploadRequest payload)
            {
                Log.LogInformation((int)EventIds.ProcessingRequest, $"Processing request to upload logs for {payload.ToJson()}");
            }

            public static void UploadLogsFinished(string id)
            {
                Log.LogInformation((int)EventIds.UploadLogsFinished, $"Finished uploading logs for module {id}");
            }

            public static void ErrorUploadingLogs(Exception ex)
            {
                Log.LogInformation((int)EventIds.ErrorUploadingLogs, ex, "Error uploading logs");
            }
        }
    }
}
