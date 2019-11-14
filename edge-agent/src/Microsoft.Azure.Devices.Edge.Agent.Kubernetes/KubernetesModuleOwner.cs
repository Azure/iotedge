// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class KubernetesModuleOwner
    {
        [JsonConstructor]
        public KubernetesModuleOwner(string apiVersion, string kind, string name, string uid)
        {
            this.ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.Kind = Preconditions.CheckNonWhiteSpace(kind, nameof(kind));
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
            this.Uid = Preconditions.CheckNonWhiteSpace(uid, nameof(uid));
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
