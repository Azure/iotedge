// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    class EdgeDeploymentDefinition
    {
        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }


        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; }

        [JsonProperty(PropertyName = "spec")]
        public IList<KubernetesModule> Spec { get; }

        public EdgeDeploymentDefinition(string apiVersion, string kind, V1ObjectMeta metadata, IList<KubernetesModule> spec)
        {
            ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            Kind = Preconditions.CheckNonWhiteSpace(kind, nameof(kind));
            Metadata = Preconditions.CheckNotNull(metadata, nameof(metadata));
            Spec = Preconditions.CheckNotNull(spec, nameof(spec));
        }
    }
}
