// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public class Endpoint
    {
        public Endpoint(EndpointType type, string deviceId)
        {
            this.EndpointType = type;
            this.DeviceId = deviceId;
        }

        public EndpointType EndpointType { get; }

        public string DeviceId { get; }
    }
}
