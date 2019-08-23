// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class LogConfig
    {
        [DataMember(Name = "Config", EmitDefaultValue = false)]
        public IDictionary<string, string> Config { get; set; }

        [DataMember(Name = "Type", EmitDefaultValue = false)]
        public string Type { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherProperties { get; set; }
    }
}
