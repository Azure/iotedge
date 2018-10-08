// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
