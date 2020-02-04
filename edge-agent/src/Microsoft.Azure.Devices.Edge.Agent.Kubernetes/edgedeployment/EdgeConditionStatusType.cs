// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EdgeConditionStatusType
    {
        [EnumMember(Value = "True")]
        True,

        [EnumMember(Value = "False")]
        False,

        [EnumMember(Value = "Unknown")]
        Unknown,
    }
}
