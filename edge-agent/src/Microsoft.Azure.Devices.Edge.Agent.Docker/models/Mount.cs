// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class Mount
    {
        [DataMember(Name = "ReadOnly", EmitDefaultValue = false)]
        public bool ReadOnly { get; set; }

        [DataMember(Name = "Source", EmitDefaultValue = false)]
        public string Source { get; set; }

        [DataMember(Name = "Target", EmitDefaultValue = false)]
        public string Target { get; set; }

        [DataMember(Name = "Type", EmitDefaultValue = false)]
        public string Type { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherProperties;
    }
}
