// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeDeploymentDefinition : IEdgeDeploymentDefinition
    {
        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; }

        [JsonProperty(PropertyName = "spec")]
        public IReadOnlyList<KubernetesModule> Spec { get; }

        public EdgeDeploymentDefinition(string apiVersion, string kind, V1ObjectMeta metadata, IReadOnlyList<KubernetesModule> spec)
        {
            this.ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.Kind = Preconditions.CheckNonWhiteSpace(kind, nameof(kind));
            this.Metadata = Preconditions.CheckNotNull(metadata, nameof(metadata));
            this.Spec = Preconditions.CheckNotNull(spec, nameof(spec));
        }
    }
}
