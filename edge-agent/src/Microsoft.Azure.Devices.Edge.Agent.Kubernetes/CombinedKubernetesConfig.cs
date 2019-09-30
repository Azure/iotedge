namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class CombinedKubernetesConfig
    {
        public CombinedKubernetesConfig(string image, CreatePodParameters createOptions, Option<AuthConfig> authConfig)
        {
            this.Image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            this.CreateOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.AuthConfig = authConfig;
        }

        [JsonConstructor]
        CombinedKubernetesConfig(string image, CreatePodParameters createOptions, AuthConfig auth)
        {
            this.Image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            this.CreateOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.AuthConfig = Option.Maybe(auth);
        }

        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "createOptions")]
        public CreatePodParameters CreateOptions { get; }

        [JsonProperty(Required = Required.AllowNull, PropertyName = "auth")]
        [JsonConverter(typeof(OptionConverter<AuthConfig>))]
        public Option<AuthConfig> AuthConfig { get; }
    }

    public class AuthConfig
    {
        public AuthConfig(string name)
        {
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
        }

        public string Name { get; }
    }

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
        public Option<IDictionary<string, string>> NodeSelector { get; set; }// todo check json conversion

        [JsonProperty("Resources", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<V1ResourceRequirements> Resources { get; set; }// todo check json conversion
    }
}
