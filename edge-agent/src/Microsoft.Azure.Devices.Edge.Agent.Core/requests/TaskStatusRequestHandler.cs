// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TaskStatusRequestHandler : RequestHandlerBase<TaskStatusRequest, TaskStatusResponse>
    {
        static readonly Version ExpectedSchemaVersion = new Version("1.0");

        public override string RequestName => "GetTaskStatus";

        protected override Task<Option<TaskStatusResponse>> HandleRequestInternal(Option<TaskStatusRequest> payloadOption, CancellationToken cancellationToken)
        {
            TaskStatusRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));
            if (ExpectedSchemaVersion.CompareMajorVersion(payload.SchemaVersion, "logs upload request schema") != 0)
            {
                Events.MismatchedMinorVersions(payload.SchemaVersion, ExpectedSchemaVersion);
            }

            BackgroundTaskStatus backgroundTaskStatus = BackgroundTask.GetStatus(payload.CorrelationId);
            Events.ProcessingRequest(payload, backgroundTaskStatus);
            return Task.FromResult(Option.Some(TaskStatusResponse.Create(payload.CorrelationId, backgroundTaskStatus)));
        }

        static class Events
        {
            const int IdStart = AgentEventIds.TaskStatusRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TaskStatusRequestHandler>();

            enum EventIds
            {
                MismatchedMinorVersions = IdStart,
                ProcessingRequest
            }

            public static void MismatchedMinorVersions(string payloadSchemaVersion, Version expectedSchemaVersion)
            {
                Log.LogWarning((int)EventIds.MismatchedMinorVersions, $"Logs upload request schema version {payloadSchemaVersion} does not match expected schema version {expectedSchemaVersion}. Some settings may not be supported.");
            }

            public static void ProcessingRequest(TaskStatusRequest payload, BackgroundTaskStatus backgroundTaskStatus)
            {
                Log.LogInformation((int)EventIds.ProcessingRequest, $"Handling status request for task {payload.CorrelationId} - {backgroundTaskStatus.ToJson()}");
            }
        }
    }
}
