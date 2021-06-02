// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class Ulimit
    {
        [JsonProperty("Name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Name { get; set; }

        [JsonProperty("Hard", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long Hard { get; set; }

        [JsonProperty("Soft", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long Soft { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
