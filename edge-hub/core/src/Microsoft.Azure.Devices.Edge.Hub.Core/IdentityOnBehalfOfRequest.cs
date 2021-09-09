// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// This is an object to encapsulate JSON serialization of the
    /// GetDeviceAndModuleOnBehalfOf POST request.
    /// </summary>
    public class IdentityOnBehalfOfRequest
    {
        [JsonProperty(PropertyName = "targetDeviceId", Required = Required.Always)]
        public string TargetDeviceId { get; }

        [JsonProperty(PropertyName = "targetModuleId", NullValueHandling = NullValueHandling.Ignore)]
        public string TargetModuleId { get; }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; }

        public IdentityOnBehalfOfRequest(string targetDeviceId, string targetModuleId, string authChain)
        {
            this.TargetDeviceId = Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));
            this.TargetModuleId = targetModuleId;
            this.AuthChain = Preconditions.CheckNonWhiteSpace(authChain, nameof(authChain));
        }
    }
}
