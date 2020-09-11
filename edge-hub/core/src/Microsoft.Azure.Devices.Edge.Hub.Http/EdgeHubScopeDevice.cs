// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    public class EdgeHubScopeDevice
    {
        public EdgeHubScopeDevice(
            string deviceId,
            string generationId,
            DeviceStatus status,
            AuthenticationMechanism authentication,
            DeviceCapabilities capabilities,
            string deviceScope,
            IEnumerable<string> parentScopes)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.GenerationId = Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            this.Status = Preconditions.CheckNotNull(status);
            this.Authentication = Preconditions.CheckNotNull(authentication);
            this.Capabilities = Preconditions.CheckNotNull(capabilities);
            this.Scope = deviceScope;
            this.ParentScopes = parentScopes;
        }

        [JsonProperty(PropertyName = "deviceId")]
        public string Id { get; }

        [JsonProperty(PropertyName = "generationId")]
        public string GenerationId { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "status")]
        public DeviceStatus Status { get; }

        [JsonProperty(PropertyName = "authentication")]
        public AuthenticationMechanism Authentication { get; }

        [JsonProperty(PropertyName = "capabilities")]
        public virtual DeviceCapabilities Capabilities { get; }

        [JsonProperty(PropertyName = "deviceScope")]
        public virtual string Scope { get; }

        [JsonProperty(PropertyName = "parentScopes")]
        public virtual IEnumerable<string> ParentScopes { get; }
    }
}
