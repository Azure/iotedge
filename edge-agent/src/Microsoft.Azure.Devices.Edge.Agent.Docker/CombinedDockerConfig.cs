// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;
    using AuthConfig = global::Docker.DotNet.Models.AuthConfig;

    public class CombinedDockerConfig
    {
        public CombinedDockerConfig(string image, CreateContainerParameters createOptions, Option<string> digest, Option<AuthConfig> authConfig)
        {
            this.Image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            this.CreateOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.Digest = digest;
            this.AuthConfig = authConfig;
        }

        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "createOptions")]
        public CreateContainerParameters CreateOptions { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "digest")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> Digest { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "auth")]
        [JsonConverter(typeof(OptionConverter<AuthConfig>))]
        public Option<AuthConfig> AuthConfig { get; }
    }
}
