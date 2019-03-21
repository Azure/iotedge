// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    public class LogsStreamRequest
    {
        public LogsStreamRequest(string schemaVersion, string id)
        {
            this.SchemaVersion = schemaVersion;
            this.Id = id;
        }

        public string SchemaVersion { get; }

        public string Id { get; }
    }
}
