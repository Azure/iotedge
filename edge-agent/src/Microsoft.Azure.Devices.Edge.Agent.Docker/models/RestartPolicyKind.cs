// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using Newtonsoft.Json;

    public enum RestartPolicyKind
    {
        [JsonProperty("")]
        Undefined,

        [JsonProperty("no")]
        No,

        [JsonProperty("always")]
        Always,

        [JsonProperty("on-failure")]
        OnFailure,

        [JsonProperty("unless-stopped")]
        UnlessStopped
    }
}
