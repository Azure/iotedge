// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System.Collections.Generic;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Newtonsoft.Json;

    interface IEdgeDeploymentDefinition<TConfig> : IKubernetesObject
    {
        [JsonProperty(PropertyName = "metadata")]
        V1ObjectMeta Metadata { get; }

        [JsonProperty(PropertyName = "spec")]
        IList<KubernetesModule<TConfig>> Spec { get; }
    }
}
