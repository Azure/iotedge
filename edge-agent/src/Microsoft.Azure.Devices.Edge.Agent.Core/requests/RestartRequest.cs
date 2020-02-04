// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class RestartRequest
    {
        public RestartRequest(string schemaVersion, string id)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
        }

        public string SchemaVersion { get; }
        public string Id { get; }
    }
}
