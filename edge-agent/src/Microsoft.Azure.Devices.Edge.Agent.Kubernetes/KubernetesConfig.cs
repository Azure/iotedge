// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class KubernetesConfig
    {
        public KubernetesConfig(string image, CreatePodParameters createOptions, Option<AuthConfig> authConfig)
        {
            this.Image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            this.CreateOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.AuthConfig = authConfig;
        }

        [JsonConstructor]
        KubernetesConfig(string image, CreatePodParameters createOptions, AuthConfig auth)
            : this(image, createOptions, Option.Maybe(auth))
        {
        }

        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "createOptions")]
        public CreatePodParameters CreateOptions { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "auth")]
        [JsonConverter(typeof(OptionConverter<AuthConfig>))]
        public Option<AuthConfig> AuthConfig { get; }
    }
}
