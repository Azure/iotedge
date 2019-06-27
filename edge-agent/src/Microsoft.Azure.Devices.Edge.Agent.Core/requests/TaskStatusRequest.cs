// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class TaskStatusRequest
    {
        public TaskStatusRequest(string schemaVersion, string correlationId)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.CorrelationId = Preconditions.CheckNonWhiteSpace(correlationId, nameof(correlationId));
        }

        public string SchemaVersion { get; }

        public string CorrelationId { get; }
    }
}
