// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class RestartPolicy
    {
        [JsonProperty("Name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public RestartPolicyKind Name { get; set; }

        [JsonProperty("MaximumRetryCount", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public long MaximumRetryCount { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
