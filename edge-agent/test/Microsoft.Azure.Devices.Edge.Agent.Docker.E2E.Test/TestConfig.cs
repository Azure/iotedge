// Copyright (c) Microsoft. All rights reserved.
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Edge.Util.Json;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    public class TestConfig
    {
        [JsonConstructor]
        public TestConfig(string name, string version, string image, string imageCreateOptions, Validator validator, PullPolicyTestConfig pullPolicyTestConfig)
        {
            this.Name = name;
            this.Version = version;
            this.Image = image;
            this.ImageCreateOptions = imageCreateOptions;
            this.Validator = validator;
            this.PullPolicyTestConfig = Option.Maybe(pullPolicyTestConfig);
        }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "image")]
        public string Image { get; set; }

        [JsonProperty(PropertyName = "imageCreateOptions")]
        public string ImageCreateOptions { get; set; }

        [JsonProperty(PropertyName = "validator")]
        public Validator Validator { get; set; }

        [JsonProperty(PropertyName = "pullPolicyTestConfig")]
        [JsonConverter(typeof(OptionConverter<PullPolicyTestConfig>))]
        public Option<PullPolicyTestConfig> PullPolicyTestConfig { get; set; }
    }
}
