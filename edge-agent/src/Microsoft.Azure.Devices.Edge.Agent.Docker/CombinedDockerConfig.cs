// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class CombinedDockerConfig
    {
        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "createOptions")]
        public CreateContainerParameters CreateOptions { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "authConfig")]
        [JsonConverter(typeof(OptionConverter<AuthConfig>))]
        public Option<AuthConfig> AuthConfig { get; }

        public CombinedDockerConfig(string image, CreateContainerParameters createOptions, Option<AuthConfig> authConfig)
        {
            this.Image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            this.CreateOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.AuthConfig = authConfig;
        }
    }
}
