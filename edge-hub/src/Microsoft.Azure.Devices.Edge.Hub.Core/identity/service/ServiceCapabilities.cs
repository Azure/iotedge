// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using Newtonsoft.Json;

    public class ServiceCapabilities
    {
        [JsonConstructor]
        public ServiceCapabilities(bool iotEdge)
        {
            this.IotEdge = iotEdge;
        }

        [JsonProperty("iotEdge")]
        public bool IotEdge { get; }
    }
}
