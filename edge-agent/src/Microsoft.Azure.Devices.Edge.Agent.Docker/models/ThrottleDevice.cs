// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class ThrottleDevice
    {
        [JsonProperty("Path", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Path { get; set; }

        [JsonProperty("Rate", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ulong Rate { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
