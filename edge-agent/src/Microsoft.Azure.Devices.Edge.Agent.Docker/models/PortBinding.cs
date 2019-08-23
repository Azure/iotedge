// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class PortBinding
    {
        [DataMember(Name = "HostIP", EmitDefaultValue = false)]
        public string HostIP { get; set; }

        [DataMember(Name = "HostPort", EmitDefaultValue = false)]
        public string HostPort { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherProperties { get; set; }
    }
}
