// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Runtime.Serialization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RestartPolicyKind
    {
        [EnumMember(Value ="")]
        Undefined,

        [EnumMember(Value = "no")]
        No,

        [EnumMember(Value = "always")]
        Always,

        [EnumMember(Value = "on-failure")]
        OnFailure,

        [EnumMember(Value = "unless-stopped")]
        UnlessStopped
    }
}
