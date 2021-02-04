// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class DeviceMapping
    {
        [JsonProperty("PathOnHost", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string PathOnHost { get; set; }

        [JsonProperty("PathInContainer", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string PathInContainer { get; set; }

        [JsonProperty("CgroupPermissions", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string CgroupPermissions { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
