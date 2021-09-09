// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class EdgeHubScopeModule
    {
        public EdgeHubScopeModule(
            string moduleId,
            string deviceId,
            string generationId,
            AuthenticationMechanism authentication)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.GenerationId = Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            this.Authentication = Preconditions.CheckNotNull(authentication);
        }

        [JsonProperty(PropertyName = "moduleId")]
        public string Id { get; }

        [JsonProperty(PropertyName = "deviceId")]
        public string DeviceId { get; }

        [JsonProperty(PropertyName = "generationId")]
        public string GenerationId { get; }

        [JsonProperty(PropertyName = "authentication")]
        public AuthenticationMechanism Authentication { get; }
    }
}
