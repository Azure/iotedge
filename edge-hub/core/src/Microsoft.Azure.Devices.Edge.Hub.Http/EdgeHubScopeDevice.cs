// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
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
        public string Id { get; set; }

        [JsonProperty(PropertyName = "generationId")]
        public string GenerationId { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "status")]
        public DeviceStatus Status { get; set; }

        [JsonProperty(PropertyName = "authentication")]
        public AuthenticationMechanism Authentication { get; set; }

        [JsonProperty(PropertyName = "capabilities", NullValueHandling = NullValueHandling.Ignore)]
        public virtual DeviceCapabilities Capabilities { get; set; }

        [JsonProperty(PropertyName = "deviceScope", NullValueHandling = NullValueHandling.Ignore)]
        public virtual string Scope { get; set; }

        [JsonProperty(PropertyName = "parentScopes", NullValueHandling = NullValueHandling.Ignore)]
        public virtual IEnumerable<string> ParentScopes { get; set; }
    }
}
