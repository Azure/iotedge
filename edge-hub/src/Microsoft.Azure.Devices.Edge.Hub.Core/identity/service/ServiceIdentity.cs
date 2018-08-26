// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ServiceIdentity
    {
        public ServiceIdentity(
            string deviceId,
            string generationId,
            IEnumerable<string> capabilities,
            ServiceAuthentication authentication,
            ServiceIdentityStatus status)
            : this(deviceId, null, generationId, capabilities, authentication, status)
        {
        }

        [JsonConstructor]
        public ServiceIdentity(
            string deviceId,
            string moduleId,
            string generationId,
            IEnumerable<string> capabilities,
            ServiceAuthentication authentication,
            ServiceIdentityStatus status)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Option.Maybe(moduleId);
            this.Capabilities = Preconditions.CheckNotNull(capabilities, nameof(capabilities));
            this.Authentication = Preconditions.CheckNotNull(authentication, nameof(authentication));
            this.Id = this.ModuleId.Map(m => $"{deviceId}/{moduleId}").GetOrElse(deviceId);
            this.GenerationId = Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            this.Status = status;
        }

        [JsonIgnore]
        public string Id { get; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; }

        [JsonProperty("moduleId")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> ModuleId { get; }

        [JsonProperty("capabilities")]
        public IEnumerable<string> Capabilities { get; }

        [JsonIgnore]
        public bool IsModule => this.ModuleId.HasValue;

        [JsonProperty("authentication")]
        public ServiceAuthentication Authentication { get; }

        [JsonProperty("status")]
        public ServiceIdentityStatus Status { get; }

        [JsonProperty("generationId")]
        public string GenerationId { get; }
    }
}
