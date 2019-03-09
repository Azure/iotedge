// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    public class LogsStreamingRequest
    {
        public LogsStreamingRequest(string schemaVersion, string id)
        {
            this.SchemaVersion = schemaVersion;
            this.Id = id;
        }

        public string SchemaVersion { get; }
        public string Id { get; }
    }
}
