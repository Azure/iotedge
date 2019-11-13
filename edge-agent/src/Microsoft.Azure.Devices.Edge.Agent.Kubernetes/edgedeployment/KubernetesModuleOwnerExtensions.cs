// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;

    public static class KubernetesModuleOwnerExtensions
    {
        public static List<V1OwnerReference> ToOwnerReferences(this KubernetesModuleOwner kubeModuleOwner) =>
            new List<V1OwnerReference>
            {
                new V1OwnerReference(
                    apiVersion: kubeModuleOwner.ApiVersion,
                    kind: kubeModuleOwner.Kind,
                    name: kubeModuleOwner.Name,
                    uid: kubeModuleOwner.Uid)
            };
    }
}
