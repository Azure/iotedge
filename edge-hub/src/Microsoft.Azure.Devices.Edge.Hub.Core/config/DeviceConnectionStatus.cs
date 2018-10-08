// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;

    using Microsoft.Azure.Devices.Client;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    class DeviceConnectionStatus
    {
        public DeviceConnectionStatus(ConnectionStatus status, DateTime? lastConnectedTimeUtc, DateTime? lastDisconnectedTimeUtc)
        {
            this.Status = status;
            this.LastConnectedTimeUtc = lastConnectedTimeUtc;
            this.LastDisconnectTimeUtc = lastDisconnectedTimeUtc;
        }

        [JsonProperty(PropertyName = "lastConnectedTimeUtc", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastConnectedTimeUtc { get; }

        [JsonProperty(PropertyName = "lastDisconnectedTimeUtc", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastDisconnectTimeUtc { get; }

        [JsonProperty(PropertyName = "status")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ConnectionStatus Status { get; }
    }
}
