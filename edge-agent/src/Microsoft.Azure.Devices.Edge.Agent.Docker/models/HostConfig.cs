// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class HostConfig
    {
        [JsonProperty("Binds", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> Binds { get; set; }

        [JsonProperty("LogConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public LogConfig LogConfig { get; set; }

        [JsonProperty("Mounts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<Mount> Mounts { get; set; }

        [JsonProperty("NetworkMode", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string NetworkMode { get; set; }

        [JsonProperty("PortBindings", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, IList<PortBinding>> PortBindings { get; set; }

        [JsonProperty("Privileged", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Privileged { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
