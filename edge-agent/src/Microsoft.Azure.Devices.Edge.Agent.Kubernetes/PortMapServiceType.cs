namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PortMapServiceType
    {
        [EnumMember(Value = "ClusterIP")]
        ClusterIP,

        [EnumMember(Value = "LoadBalancer")]
        LoadBalancer,

        [EnumMember(Value = "NodePort")]
        NodePort,

    }
}