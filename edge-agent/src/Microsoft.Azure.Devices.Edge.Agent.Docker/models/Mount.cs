// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class Mount
    {
        [JsonProperty("ReadOnly", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ReadOnly { get; set; }

        [JsonProperty("Source", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Source { get; set; }

        [JsonProperty("Target", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Target { get; set; }

        [JsonProperty("Type", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Type { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
