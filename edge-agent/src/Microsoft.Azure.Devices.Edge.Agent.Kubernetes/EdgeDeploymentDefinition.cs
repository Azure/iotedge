// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    class EdgeDeploymentDefinition<TConfig> : IEdgeDeploymentDefinition<TConfig>
    {
        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; }

        [JsonProperty(PropertyName = "spec")]
        public IList<KubernetesModule<TConfig>> Spec { get; }

        public EdgeDeploymentDefinition(string apiVersion, string kind, V1ObjectMeta metadata, IList<KubernetesModule<TConfig>> spec)
        {
            this.ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.Kind = Preconditions.CheckNonWhiteSpace(kind, nameof(kind));
            this.Metadata = Preconditions.CheckNotNull(metadata, nameof(metadata));
            this.Spec = Preconditions.CheckNotNull(spec, nameof(spec));
        }
    }
}
