// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class CreateContainerParameters
    {
        [JsonProperty("Env", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> Env { get; set; }

        [JsonProperty("ExposedPorts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> ExposedPorts { get; set; }

        [JsonProperty("HostConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public HostConfig HostConfig { get; set; }

        [JsonProperty("Image", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Image { get; set; }

        [JsonProperty("Labels", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, string> Labels { get; set; }

        [JsonProperty("Name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Name { get; set; }

        [JsonProperty("NetworkingConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public NetworkingConfig NetworkingConfig { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
