// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class EndpointSettings
    {
        [JsonProperty("Aliases", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> Aliases { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
