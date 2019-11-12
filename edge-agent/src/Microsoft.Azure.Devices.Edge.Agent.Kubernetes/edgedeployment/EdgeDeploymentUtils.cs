// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System.Collections.Generic;
    using k8s.Models;

    public class EdgeDeploymentUtils
    {
        public static List<V1OwnerReference> KubeModuleOwnerToOwnerReferences(KubernetesModuleOwner kubeModuleOwner) =>
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
