// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class NetworkingConfig
    {
        [DataMember(Name = "EndpointsConfig", EmitDefaultValue = false)]
        public IDictionary<string, EndpointSettings> EndpointsConfig { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherProperties { get; set; }
    }
}
