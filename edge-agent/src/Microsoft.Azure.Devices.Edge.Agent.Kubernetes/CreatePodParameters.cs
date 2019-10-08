// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class CreatePodParameters
    {
        public CreatePodParameters(
            IList<string> env,
            IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> exposedPorts,
            HostConfig hostConfig,
            string image,
            IDictionary<string, string> labels,
            NetworkingConfig networkingConfig)
            : this(env, exposedPorts, hostConfig, image, labels, networkingConfig, null, null)
        {
        }

        [JsonConstructor]
        CreatePodParameters(
            IList<string> env,
            IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> exposedPorts,
            HostConfig hostConfig,
            string image,
            IDictionary<string, string> labels,
            NetworkingConfig networkingConfig,
            IDictionary<string, string> nodeSelector,
            V1ResourceRequirements resources)
        {
            this.Env = Option.Maybe(env);
            this.ExposedPorts = Option.Maybe(exposedPorts);
            this.HostConfig = Option.Maybe(hostConfig);
            this.Image = Option.Maybe(image);
            this.Labels = Option.Maybe(labels);
            this.NetworkingConfig = Option.Maybe(networkingConfig);
            this.NodeSelector = Option.Maybe(nodeSelector);
            this.Resources = Option.Maybe(resources);
        }

        [JsonProperty("Env", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IList<string>>))]
        public Option<IList<string>> Env { get; }

        [JsonProperty("ExposedPorts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IDictionary<string, global::Docker.DotNet.Models.EmptyStruct>>))]
        public Option<IDictionary<string, global::Docker.DotNet.Models.EmptyStruct>> ExposedPorts { get; }

        [JsonProperty("HostConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<HostConfig>))]
        public Option<HostConfig> HostConfig { get; }

        [JsonProperty("Image", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> Image { get; }

        [JsonProperty("Labels", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IDictionary<string, string>>))]
        public Option<IDictionary<string, string>> Labels { get; }

        [JsonProperty("NetworkingConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<NetworkingConfig>))]
        public Option<NetworkingConfig> NetworkingConfig { get; }

        [JsonProperty("NodeSelector", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<IDictionary<string, string>>))]
        public Option<IDictionary<string, string>> NodeSelector { get; set; }

        [JsonProperty("Resources", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(OptionConverter<V1ResourceRequirements>))]
        public Option<V1ResourceRequirements> Resources { get; set; }
    }
}
