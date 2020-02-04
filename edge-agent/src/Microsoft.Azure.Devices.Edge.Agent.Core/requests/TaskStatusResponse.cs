// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class TaskStatusResponse
    {
        public static TaskStatusResponse Create(string correlationId, BackgroundTaskStatus backgroundTaskStatus)
        {
            Preconditions.CheckNotNull(backgroundTaskStatus, nameof(backgroundTaskStatus));
            string message = string.Empty;
            if (backgroundTaskStatus.Status == BackgroundTaskRunStatus.Failed)
            {
                message = backgroundTaskStatus.Exception.Match(
                    e => $"Task {backgroundTaskStatus.Operation} failed because of error {e.Message}",
                    () => $"Task {backgroundTaskStatus.Operation} failed with no error");
            }

            return new TaskStatusResponse(correlationId, backgroundTaskStatus.Status, message);
        }

        public TaskStatusResponse(string correlationId, BackgroundTaskRunStatus status, string message)
        {
            this.CorrelationId = Preconditions.CheckNonWhiteSpace(correlationId, nameof(correlationId));
            this.Status = status;
            this.Message = Preconditions.CheckNotNull(message, nameof(message));
        }

        [JsonProperty("status")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BackgroundTaskRunStatus Status { get; }

        [JsonProperty("message")]
        public string Message { get; }

        [JsonProperty("correlationId")]
        public string CorrelationId { get; }
    }
}
