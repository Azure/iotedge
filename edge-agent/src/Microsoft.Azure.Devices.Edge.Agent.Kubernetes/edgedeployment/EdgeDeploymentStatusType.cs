// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EdgeDeploymentStatusType
    {
        [EnumMember(Value = "Success")]
        Success,

        [EnumMember(Value = "Failure")]
        Failure,
    }
}
