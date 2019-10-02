// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class CreatePodParameters
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

        [JsonProperty("NetworkingConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public NetworkingConfig NetworkingConfig { get; set; }

        [JsonProperty("NodeSelector", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<IDictionary<string, string>> NodeSelector { get; set; } = Option.None<IDictionary<string, string>>();
    }
}
