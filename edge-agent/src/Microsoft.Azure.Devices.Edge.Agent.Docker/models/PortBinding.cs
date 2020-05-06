// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class PortBinding
    {
        [JsonProperty("HostIP", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string HostIP { get; set; }

        [JsonProperty("HostPort", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string HostPort { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
