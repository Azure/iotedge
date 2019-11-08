// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class KubernetesModuleOwner
    {
        [JsonConstructor]
        public KubernetesModuleOwner(string apiVersion, string kind, string name, string uid)
        {
            this.ApiVersion = apiVersion;
            this.Kind = kind;
            this.Name = name;
            this.Uid = uid;
        }

        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; }

        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; }

        [JsonProperty(PropertyName = "uid")]
        public string Uid { get; }
    }
}
