// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ServiceIdentity
    {
        public ServiceIdentity(string deviceId, bool isEdgeDevice, ServiceAuthentication serviceAuthentication)
            : this(deviceId, null, isEdgeDevice, serviceAuthentication)
        {
        }

        [JsonConstructor]
        public ServiceIdentity(string deviceId, string moduleId, bool isEdgeDevice, ServiceAuthentication authentication)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Option.Maybe(moduleId);
            this.IsEdgeDevice = isEdgeDevice;
            this.Authentication = Preconditions.CheckNotNull(authentication, nameof(authentication));
            this.Id = this.ModuleId.Map(m => $"{deviceId}/{moduleId}").GetOrElse(deviceId);
        }

        [JsonIgnore]
        public string Id { get; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; }

        [JsonProperty("moduleId")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> ModuleId { get; }

        [JsonProperty("edge")]
        public bool IsEdgeDevice { get; }

        [JsonIgnore]
        public bool IsModule => this.ModuleId.HasValue;

        [JsonProperty("authentication")]
        public ServiceAuthentication Authentication { get; }
    }
}
