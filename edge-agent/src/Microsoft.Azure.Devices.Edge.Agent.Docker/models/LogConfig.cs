// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class LogConfig
    {
        [JsonProperty("Config", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, string> Config { get; set; }

        [JsonProperty("Type", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Type { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
