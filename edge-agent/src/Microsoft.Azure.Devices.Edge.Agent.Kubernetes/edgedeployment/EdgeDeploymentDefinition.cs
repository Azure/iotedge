// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
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

        [JsonProperty(PropertyName = "status")]
        [JsonConverter(typeof(OptionConverter<EdgeDeploymentStatus>))]
        public Option<EdgeDeploymentStatus> Status { get; }

        public EdgeDeploymentDefinition(string apiVersion, string kind, V1ObjectMeta metadata, IList<KubernetesModule> spec, EdgeDeploymentStatus status = null)
        {
            this.ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.Kind = Preconditions.CheckNonWhiteSpace(kind, nameof(kind));
            this.Metadata = Preconditions.CheckNotNull(metadata, nameof(metadata));
            this.Spec = Preconditions.CheckNotNull(spec, nameof(spec));
            this.Status = Option.Maybe(status);
        }
    }
}
